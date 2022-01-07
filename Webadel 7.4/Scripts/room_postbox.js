$(document).ready(function () {
    autosize($("#postForm #postbox"));

    $("#insertCutTag").click(function () { AppendText("<div class=\"cut\" title=\"Click to reveal hidden secret text.\"> Put the text you want to hide here. </div>\r\n"); });
    $("#insertSpoiler").click(function () { AppendText("<span class=\"spoiler\"> Put your spoiler text here. </span>"); });
    $("#insertImageCode").click(function () { AppendText("<img src=\"imageUrl\">\r\n"); });
    $("#insertHyperlink").click(function () { AppendText("<a href=\"linkUrl\">Link Title</a>\r\n"); });

    $("#anonymous").click(function () {
        var anon = "Anonymous"; // get this from system config

        // #postAnonymously
        if ($("#postForm #postAnonymously").val() == "True") {
            $("#postForm #postAnonymously").val("False");
            $("#postForm .author").html($("#postForm .author").data("original"));
        } else {
            $("#postForm #postAnonymously").val("True");
            $("#postForm .author").html(anon);
        }
    });

    $("#attachResource").click(function () { $("#uploadResource").click(); });
    $("#uploadResource").fileupload({
        dataType: "json", done:
            function (e, data) {
                $("#resources table tbody").append("<tr><td><a href='/Res/" + data.result.Filename + "' target='_blank'>" + data.result.Filename + "</a><input type='hidden' name='files' value='" + data.result.Id + "' /></td><td><button type='button' class='close deleteFile' aria-hidden='true'>&times;</button></td></tr>");
            }
    });

    $("#resources").on("click", ".deleteFile", function () { $(this).parents("tr").remove(); });

    $("#post").click(function () { if (_pageReady) Post(); });
    $("#postGoto").click(function () { if (_pageReady) PostGoto(); });
    $("#preview").click(function () { if (_pageReady) Preview(); });

    $("#messages").on("click", ".quote", function () {
        var lines = $(this).parents(".message").find(".body").text().split("\n");
        var quotedMessage = "";
        for (var i = 0; i < lines.length; i++) quotedMessage += (lines[i] == "" ? "" : "> " + lines[i]) + "\n";
        AppendText(quotedMessage + "\n");
    });

    $("#messages").on("click", ".reply", function () {
        var recipientId = $(this).parents(".message").find(".author").attr("id");
        $("#postForm #recipientId").val(recipientId);
        $("#postForm #postbox").focus();
    });

    Mousetrap.bind("ctrl+enter", function (e) { if (IsPostbox(e)) Preview(); });
    Mousetrap.bind("shift+ctrl+enter", function (e) { if (IsPostbox(e)) Post(); });
    Mousetrap.bind("ctrl+b", function (e) { WrapText(e, "b"); });
    Mousetrap.bind("ctrl+i", function (e) { WrapText(e, "i"); });
    Mousetrap.bind("ctrl+u", function (e) { WrapText(e, "u"); });
    Mousetrap.bind("ctrl+s", function (e) { WrapText(e, "s"); });
    Mousetrap.bind("ctrl+p", function (e) { WrapText(e, "pre"); });
    Mousetrap.bind("ctrl+l", function (e) { WrapText_Anchor(e); });

    // update post box's message header's time stamp
    setInterval(function () {
        $("#postForm .time").text(CitDate(new Date()));
    }, 60000); // this won't work outside of CST - calculate the "time offset" and use that to modify the local time display

    // catch changes in postbox and autosave them in case of system failure
    setInterval(function () {
        if ($("#postbox").data("prev") == undefined) $("#postbox").data("prev", $("#postbox").val());

        if ($("#postbox").val() != $("#postbox").data("prev")) {
            // console.log($("#postbox").val());
            $.post("/Room/Autosave?roomId=" + _roomId, $("#postForm").serialize());
            $("#postbox").data("prev", $("#postbox").val());
        }
    }, 1000);
});

// append "text" to the end of the postbox (and resize it)
function AppendText(text) {
    $("#postForm #postbox").val($("#postForm #postbox").val() + text).focus();
    autosize.update($("#postForm #postbox"));
}

function WrapText(e, tag) {
    if (!_textareaHasFocus) return;

    $(e.target).textrange("replace", "<" + tag + ">" + $(e.target).textrange("get").text + "</" + tag + ">");
    e.preventDefault();
}

function WrapText_Anchor(e, tag) {
    if (!_textareaHasFocus) return;

    var url = $(e.target).textrange("get").text;
    $(e.target).textrange("replace", "<a href=\"" + url + "\">" + url.replace(/^.*:\/\//, "") + "</a>");
    e.preventDefault();
}

// used with mousetrap to determine if the keypress came from the postbox or not
function IsPostbox(e) { return e.target == $("#postForm #postbox")[0]; }

function Preview() {
    if ($("#postForm textarea").val() == "" && $("#postForm #resources tr").length == 0) return;

    $.post("/Room/PreviewMessage?roomId=" + _roomId, $("#postForm").serialize(), function (msg) {
        var previewMsgDom = CreateMessageDom(msg);
        previewMsgDom.find(".messageControls").remove();
        previewMsgDom.find(".messageheader").removeClass("text-info"); // remove the poorly contrasting header color
        $("#messagePreview").hide().html(previewMsgDom).fadeIn("fast");
        $("#postForm textarea").focus();
    });
}

var _readyToPost = true;
function Post(onSuccessCallback) {
    if (!_readyToPost) return;
    if ($("#postForm textarea").val() == "" && $("#postForm #resources tr").length == 0) return;

    if ($("#postForm .recipient").is(":visible") && $("#postForm .recipient").val() == "") {
        alert("You must choose a recipient.");
        return;
    }

    _readyToPost = false;
    $.post("/Room/PostMessage?roomId=" + _roomId, $("#postForm").serialize(), function (msg) {
        $("#messagePreview").hide();
        var newMsgDom = CreateMessageDom(msg);
        // newMsgDom.find(".votingButtons").css('display', 'inline-block'); // no, you can't vote on your own messages
        $("#messages").append(newMsgDom);
        ResetPostbox();
        if (onSuccessCallback != null) onSuccessCallback();
        FrequentInterval();
        ShowDeletableMessages();
    });
}

function PostGoto() {
    Post(function () { Goto(); });
}

function ResetPostbox() {
    $("#postForm #postbox").val("").blur().css("height", ""); // remove the height proprerty to return a previously autosized textarea back to normal
    $("#postbox").data("prev", $("#postbox").val()) // reset autosave
    $("#resources tr").remove();
    $("#postForm").show();
    $("#predictive").hide();

    // reset the "anonymous" bit
    $("#postForm .author").html($("#postForm .author").data("original"));
    $("#postForm #postAnonymously").val("False");

    _readyToPost = true;
}