using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;
using Myriads;

namespace Webadel7 {
    public partial class Resource {
        public readonly Guid Id;
        public Guid? MessageId;
        public string Filename, ContentType;
        public readonly DateTime Uploaded;

        public bool IsImage => ContentType.StartsWith("image");
        public Message Message => MessageId.HasValue ? new Message(MessageId.Value, false) : null;

        public string Filepath => GetResourceDir() + @"\" + Id.ToString("D");


        public FileInfo FileInfo => new FileInfo(Filepath);

        protected Resource(DB.Resource resource) {
            Id = resource.id;
            MessageId = resource.messageId;
            Filename = resource.filename;
            ContentType = resource.contentType.ToLower();
            Uploaded = resource.uploaded;
        }

        /// <summary> Load and return the data from the file system (resized and cropped if specified and if IsImage). </summary>
        public byte[] GetData(int? width = null, float? aspectRatio = null) {
            try {
                byte[] source = File.ReadAllBytes(Filepath); // r.data.ToArray();			

                // crop out-of-range sections
                if (aspectRatio.HasValue) source = Crop(source, aspectRatio.Value);

                // source: http://www.amersafi.com/post/Very-High-Quality-Image-Resizing-in-NET.aspx
                if (width.HasValue && IsImage) source = Resize(source, width.Value);

                return source;

            } catch {
                // file not found or whatever; this will be quite common during dev 
                return new byte[0];
            }
        }

        public void Save() {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            DB.Resource r = dc.Resources.Single(o => o.id == Id);
            r.messageId = MessageId;
            r.filename = Filename;
            r.contentType = ContentType;
            dc.SubmitChanges();

            Cache.Remove("Resources");
        }

        public static Resource Load(Guid resourceId) {
            return GetAll().SingleOrDefault(o => o.Id == resourceId);
        }

        public static Resource Load(string filename) {
            return GetAll().SingleOrDefault(o => o.Filename == filename);
        }

        public static List<Resource> GetAll() {
            return (List<Resource>)Myriads.Cache.Get("Resources", "all", delegate () {
                DB.WebadelDataContext dc = new DB.WebadelDataContext();
                return dc.Resources.Select(o => new Resource(o)).ToList();
            });
        }

        public static Resource UploadResource(HttpPostedFileBase upload) {
            if (upload == null) return null;

            byte[] b = new byte[upload.ContentLength];
            upload.InputStream.Read(b, 0, upload.ContentLength);

            return Resource.UploadResource(upload.FileName, upload.ContentType, b);
        }

        public static Resource UploadResource(string filename, string contentType, byte[] data) {
            if (string.IsNullOrWhiteSpace(filename) || string.IsNullOrWhiteSpace(contentType) || data == null || data.Length == 0) return null;

            filename = filename.Replace("%", "_"); // routes cannot have percent signs, so /Res/File%25Name.jpg will fail [jj 12Aug7]

            DB.WebadelDataContext dc = new DB.WebadelDataContext();

            DB.Resource res = new DB.Resource() {
                id = Guid.NewGuid(),
                messageId = null,
                filename = GetUniqueFilename(filename),
                contentType = contentType,
                uploaded = MvcApplication.Now
            };
            dc.Resources.InsertOnSubmit(res);
            dc.SubmitChanges();

            File.WriteAllBytes(GetResourceDir() + @"\" + res.id.ToString("D"), data);

            Cache.Remove("Resources");
            return Resource.Load(res.id);
        }

        public static void Delete(Guid resourceId) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();

            DB.Resource res = dc.Resources.SingleOrDefault(o => o.id == resourceId);
            if (res == null) return;

            dc.Resources.DeleteOnSubmit(res);
            dc.SubmitChanges();

            // physically remove files (original and any resized images)
            foreach (FileInfo oldFile in GetResourceDir().GetFiles(resourceId.ToString("D") + "*.*")) oldFile.Delete();

            Cache.Remove("Resources");
        }

        public static byte[] Crop(byte[] source, float aspectRatio) {
            Image originalImage = Image.FromStream(new MemoryStream(source));
            float originalWidth = (float)originalImage.Width, originalHeight = (float)originalImage.Height;
            float sourceAR = originalWidth / originalHeight;

            int newHeight = originalImage.Height, newWidth = originalImage.Width;
            if (sourceAR > aspectRatio) { // wide
                newWidth = (int)(originalWidth * aspectRatio / sourceAR);
            }

            if (sourceAR < aspectRatio) { // tall
                newHeight = (int)(originalHeight * sourceAR / aspectRatio);
            }

            Image cropped = new Bitmap(newWidth, newHeight);
            Graphics croppedGraphics = Graphics.FromImage(cropped);

            //croppedGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            //croppedGraphics.SmoothingMode = SmoothingMode.HighQuality;
            //croppedGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            //croppedGraphics.CompositingQuality = CompositingQuality.HighQuality;
            //croppedGraphics.DrawImage(originalImage, 0, 0, width, height);
            croppedGraphics.DrawImage(originalImage, -(originalWidth - newWidth) / 2, -(originalHeight - newHeight) / 2, originalWidth, originalHeight);

            EncoderParameters encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 100L);

            ImageCodecInfo[] info = ImageCodecInfo.GetImageEncoders();
            MemoryStream result = new MemoryStream();
            cropped.Save(result, info[1], encoderParameters);
            result.Position = 0;
            return result.ToArray();
        }

        // source: http://www.amersafi.com/post/Very-High-Quality-Image-Resizing-in-NET.aspx
        public static byte[] Resize(byte[] source, int width) {
            Image originalImage = Image.FromStream(new MemoryStream(source));

            // never resize images to make them larger
            if (originalImage.Width <= width) return source;

            int height = width * originalImage.Height / originalImage.Width;

            Image thumbnail = new Bitmap(width, height);
            Graphics thumbGraphics = Graphics.FromImage(thumbnail);
            thumbGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            thumbGraphics.SmoothingMode = SmoothingMode.HighQuality;
            thumbGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            thumbGraphics.CompositingQuality = CompositingQuality.HighQuality;
            thumbGraphics.DrawImage(originalImage, 0, 0, width, height);

            ImageCodecInfo[] info = ImageCodecInfo.GetImageEncoders();
            EncoderParameters encoderParameters;
            encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 100L);

            MemoryStream result = new MemoryStream();
            thumbnail.Save(result, info[1], encoderParameters);
            result.Position = 0;
            return result.ToArray();
        }

        /// <summary> Returns a unique filename as similar to the preferred filename as psosible. </summary>
        public static string GetUniqueFilename(string preferredFilename, Guid? excludeResourceId = null) {
            if (IsFilenameUnique(preferredFilename, excludeResourceId)) return preferredFilename;

            string baseName = Path.GetFileNameWithoutExtension(preferredFilename);
            string extension = Path.GetExtension(preferredFilename);
            int n = 2;
            while (!IsFilenameUnique(baseName + " " + n + extension, excludeResourceId)) n++;
            return baseName + " " + n + extension;
        }

        /// <summary> Checks for filename uniqueness. If excludeResourceId is not null, ignore that resource when checking for uniqueness. </summary>
        public static bool IsFilenameUnique(string filename, Guid? excludeResourceId = null) {
            List<Resource> all = GetAll();
            if (excludeResourceId.HasValue) all = all.Where(o => o.Id != excludeResourceId.Value).ToList();
            return !all.Any(o => o.Filename == filename);
        }

        /// <summary> Cull old, orphaned resource records. </summary>
        public static void CullOrphanedResources(HttpContext httpContext) {
            DateTime cullDate = MvcApplication.Now.AddMinutes(-60);

            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            IQueryable<DB.Resource> orphanedResources = dc.Resources.Where(o => !o.messageId.HasValue && o.uploaded < cullDate);
            if (orphanedResources.Count() > 0) {
                Room.PostToSystem("Orphaned resources culled:<br><br>" + string.Join("<br>", orphanedResources.Select(o => o.filename + " [" + o.uploaded + "]")), httpContext);
                dc.Resources.DeleteAllOnSubmit(orphanedResources);
                dc.SubmitChanges();
                Cache.Remove("Resources");
            }

            // remove physical files with no corresponding resource

            // something is going wrong here and I don't know what it is // let's try getting the list of resource ids directly from the db (why would this matter? It shouldn't!)
            // List<Guid> allResourceIds = GetAll().Select(o => o.Id).ToList();
            List<Guid> allResourceIds = dc.Resources.Select(o => o.id).ToList();
            List<string> orphanedFilenames = new List<string>();
            foreach (FileInfo file in GetResourceDir(httpContext).GetFiles())
                if (!allResourceIds.Contains(new Guid(file.Name.Substring(0, 36)))) {
                    orphanedFilenames.Add(file.Name + " [" + file.CreationTime + "]");
                    file.Delete();
                }
            if (orphanedFilenames.Count > 0) Room.PostToSystem("Orphaned files culled:<br><br>" + string.Join("<br>", orphanedFilenames), httpContext);
        }

        public static DirectoryInfo GetResourceDir(HttpContext httpContext = null) {
            if (httpContext == null) httpContext = HttpContext.Current;

            DirectoryInfo resDir = new DirectoryInfo(httpContext.Server.MapPath(System.Configuration.ConfigurationManager.AppSettings["ResourceDirectory"]));
            if (!resDir.Exists) resDir.Create();
            return resDir;
        }

    }

    public struct JsonResource {
        public Guid id;
        public string filename;
        public bool isImage;

        public JsonResource(Resource m) {
            id = m.Id;
            filename = m.Filename;
            isImage = m.IsImage;
        }
    }
}