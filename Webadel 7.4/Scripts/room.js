var _roomId = null; 		// current room id
var _canEdit = false; 		// user can move/delete message and edit this room
var _pageReady = false; 	// the page is ready if everything is loaded and we can accept button clicks; the page is not ready while it is loading messages (eg)
var _lastSystemCheck = 0; 	// last time we got a user/room update
var _intervalDelay = 60000; // default interval between updates
var _frequentInterval; 		// handle for the FrequentInterval() timeout
var _previousPopover = null; // handle so that we can limit user popovers to just one at a time
var _totalNewMessages = 0;  // total number of new messages
var _replacements = {};     // arbitrary string replacements (is loaded from ... app_data?)

// used to determine if the page is being viewed or is hidden behind other tabs; we can throttle the frequent updates when this is the case
var _windowFocused = true;
window.onfocus = function () {
    if (!_windowFocused) FrequentInterval();
    _windowFocused = true;
    _intervalDelay = 60 * 1000;
};
window.onblur = function () {
    _windowFocused = false;
};

$(document).ready(function () {
    Mousetrap.bind("s", function (e) { if (_pageReady && !_textareaHasFocus) Skip(); });
    Mousetrap.bind("g", function (e) { if (_pageReady && !_textareaHasFocus) Goto(); });
    Mousetrap.bind("u", function (e) { if (_pageReady && !_textareaHasFocus) Ungoto(); }); // it would be nice to catch the browser back button and make it work like this
    Mousetrap.bind("a", function (e) { if (_pageReady && !_textareaHasFocus) ShowAll(); });
    Mousetrap.bind("m", function (e) { if (_pageReady && !_textareaHasFocus) ShowMore(); });
    Mousetrap.bind("t", function (e) { if (_pageReady && !_textareaHasFocus) ShowToday(); });
    Mousetrap.bind("n", function (e) { if (_pageReady && !_textareaHasFocus) ShowNew(); });
    Mousetrap.bind("e", function (e) { if (_pageReady && !_textareaHasFocus) $("#postbox").focus(); });
    Mousetrap.bind(". s", function (e) { if (_pageReady && !_textareaHasFocus) $("#searchModal").modal("show"); });
    Mousetrap.bind(". i", function (e) { if (_pageReady && !_textareaHasFocus) $("#roomInfoModal").modal("show"); });
    Mousetrap.bind(". u", function (e) { if (_pageReady && !_textareaHasFocus) $("#userListModal").modal("show"); });
    Mousetrap.bind("l", function (e) { if (!_textareaHasFocus) window.location = "/Auth/Logout"; });
    Mousetrap.bind("?", function (e) { if (!_textareaHasFocus) $("#helpModal").modal("show"); });

    $(window).resize(function () {
        // maintain the aspect ratio of iframes
        $("iframe").each(function () { $(this).height($(this).width() * $(this).attr("height") / $(this).attr("width")); });
    });

    // while swiping support is cool, it ruins text selection on non-mobile devices
    var isMobile = (/iPhone|iPod|iPad|Android|BlackBerry/).test(navigator.userAgent); // kinda kludgy but it should get the job done // http://stackoverflow.com/questions/3514784/what-is-the-best-way-to-detect-a-handheld-device-in-jquery#comment14034239_3540295
    if (isMobile && _currentUser.enableSwipe) {
        $("#roomDisplay").swipe({
            fingers: "all",
            // excluded certain elements from swipe detection (spoiler text, username popup) [jj 14Jan29] [#2536] from: http://labs.rampinteractive.co.uk/touchSwipe/demos/Excluded_children.html
            excludedElements: $.fn.swipe.defaults.excludedElements + ", .spoiler, span.user, .showPlonk",
            swipeRight: function (event, direction, distance, duration, fingerCount) {
                if (!_pageReady || _textareaHasFocus) return;
                Ungoto();

            }, swipeLeft: function (event, direction, distance, duration, fingerCount) {
                if (!_pageReady || _textareaHasFocus) return;
                if (fingerCount == 1) Goto();
                else if (fingerCount == 2) Skip();
            }
        });
    }

    $("#helpModal").on('show.bs.modal', function () {
        CloseTopNav();
        $("#helpModal").load("/Room/Index_Help_Dialog");
    });

    $("#submitBadgeModal").on('show.bs.modal', function () {
        CloseTopNav();
        $("#submitBadgeModal").load("/Badges/Index_SubmitBadge_Dialog");
    });

    $("#newIt").change(function () {
        if ($(this).val() == "") return;

        $.post("/Room/NewIt", { newIt: $(this).val() }, function () { $("#newIt").closest(".alert").remove(); });
    });

    $("#userListModal").on('show.bs.modal', function () {
        CloseTopNav();
        $("#userListModal").load("/Room/Index_UserList_Dialog");
    });

    $("body").on("click", ".inlineyoutube", function () {
        var iframe = $(this).next();
        var iframeSrc = iframe.attr("src");

        if (iframe.is(':visible')) iframe.attr("src", iframeSrc.replace("autoplay=1", "autoplay=0")).hide();
        else iframe.attr("src", iframeSrc.replace("autoplay=0", "autoplay=1")).show();
    });

    $("#debugModal").on('show.bs.modal', function () { CloseTopNav(); });
    $("#debugModal #freqInt").click(function () { FrequentInterval(); });
    $("#debugModal #fakeError").click(function () {
        $("#debugModal").modal("hide");
        $("#errorModal #errorStatus").text("433");
        $("#errorModal #errorStatusText").text("No error");
        $("#errorModal").modal("show");
    });

    ScrollToTop();

    $("#roomInfo").hover(function () { $("#roomInfo").animate({ opacity: 1 }, "fast"); }, function () { $("#roomInfo").animate({ opacity: .3 }, "fast"); });
    $("#roomInfo").on("click", function () { $("#roomInfoModal").modal("show"); });

    // right-hand room list (for large displays)
    $("#roomList").hover(function () { $("#roomList").animate({ opacity: 1 }, "fast"); }, function () { $("#roomList").animate({ opacity: .3 }, "fast"); });
    $("#roomList").on("click", ".list-group-item", function () { LoadRoom($(this).attr("id")); });

    $("#showAll").click(function () { if (_pageReady) ShowAll(); });
    $("#showMore").click(function () { if (_pageReady) ShowMore(); });
    $("#showToday").click(function () { if (_pageReady) ShowToday(); });
    $("#showNew").click(function () { if (_pageReady) ShowNew(); });
    $("#roomDisplay").on("click", "#getNewestMessages", function () { ShowNewest(); return false; });

    $("#roomDisplay").on("click", ".revealCut", function () { $(this).hide().next().show(); return false; });
    $("#roomDisplay").on("click", ".spoiler", function () { $(this).removeClass("spoiler"); return false; });

    $("#errorModal #abort").click(function () { window.location = "/Auth/Index"; });
    $("#errorModal #retry").click(function () { $("#errorModal").modal("hide"); });
    $("#errorModal #fail").click(function () { window.location = "/Room/Index"; });

    // clicking on a username now just pops the profile dialog
    $("body").on("click", ".user", function () { ViewProfile($(this).attr("id")); });

    $("#messages").on("click", ".showPlonk", function () {
        $(this).next().show();
        $(this).remove();

    }).on("loaded", function () { // executes whenever messages are loaded 
        GetVotes();
        ShowDeletableMessages();
    });

    $("#editUserModal").on("click", "#save", function () { EditUser_Save(); });
    $("#editUserModal").on("click", "#delete", function () { EditUser_Delete(); });
    $("#editUserModal").on("click", ".removeBadge", function () { EditUser_RemoveBadge($(this)); });

    _frequentInterval = setTimeout(FrequentInterval, _intervalDelay); // many things need to happen with relative frequency; stick them all in here

    // if a room cookie exists, try to load that room; otherwise just goto // these also fire the FrequentInterval() method
    if ($.cookie("roomId") != undefined && $.cookie("roomId") != "null") LoadRoom($.cookie("roomId"));
    else Goto();
});

// if the most recent message can be deleted then display the delete button
function ShowDeletableMessages() {
    $.get("/Messages/GetLastMessage?roomId=" + _roomId, function (messageId) {
        if (messageId == null) return;

        var $message = $("#messages .message#" + messageId);

        // only for author
        var authorId = $message.find(".author").attr("id");
        if (authorId != _currentUser.id) return;

        $message.find(".messageControls .undo").show();
    });
}

function ViewProfile(id) {
    var $openModal = $(".modal:visible");

    CloseModal($openModal, function () {
        $("#viewProfileModal").load("/Room/Index_ViewProfile_Dialog?userId=" + id, function () {
            $("#viewProfileModal img").each(function () { $(this).addClass("img-responsive"); });
            $("#viewProfileModal .bio").html(QuickFormat($("#viewProfileModal .bio").html()));
            $("#viewProfileModal .bio").html(ReplaceURLWithHTMLLinks($("#viewProfileModal .bio").html()));
            $("#viewProfileModal").modal("show");
        });
    });
}

function EditUser(id) {
    $("#editUserModal").load("/Room/Index_EditUser_Dialog?userId=" + id, function () { $("#editUserModal").modal("show"); });
}

function EditUser_RemoveBadge($removeBadgeButton) {
    $.post("/Room/RemoveBadge?badgeId=" + $removeBadgeButton.data("badgeid"), $("#editUserModal form").serialize(), function (response) {
        $removeBadgeButton.prev().remove();
        $removeBadgeButton.remove();
    });
}

function EditUser_Save() {
    $.post("/Room/UpdateUser", $("#editUserModal form").serialize(), function (response) {
        if (response == "") {
            $("#editUserModal").modal("hide");
            LoadRoom(_roomId);
        } else {
            alert(response);
        }
    });
}
function EditUser_Delete() {
    if (!confirm("Are you sure you want to permanently delete this user?")) return;

    $.post("/Room/DeleteUser", $("#editUserModal form").serialize(), function () {
        $("#editUserModal").modal("hide");
        LoadRoom(_roomId);
    });
}

function SendUserMail(id) {
    LoadRoom(_mailRoomId, function () {
        $("#postForm #recipientId").val(id);
        $("#postForm #postbox").focus();
    });
}

function FrequentInterval() {
    $.get("/Auth/GetUpdate?roomId=" + _roomId, function (data, textStatus, jqXHR) {
        if (data.error == "nosession") window.location = "/Auth/Index?lostsession=true"; // this will only happen if the auth token cookie is missing

        _totalNewMessages = data.totalNew;
        $("#totalNew").text(_totalNewMessages);
        $("#totalNew2").text(_totalNewMessages); // this one is in the bottom nav bar for xs displays
        UpdatePageTitle(null);

        UpdateNewMessageCount(data);

        if (data.lastSystemChange > _lastSystemCheck) {
            //			console.log("data.lastSystemChange: " + data.lastSystemChange);
            //			console.log("_lastSystemCheck: " + _lastSystemCheck);

            $("#postForm #recipientId").load("/Room/Index_MailRecipientSelect");
            $("#roomList").load("/Room/Index_RoomList", function () {
                // repeat this routine because we just cleared that DOM element
                UpdateNewMessageCount(data);
            });
            $.get("/Content/replacements.json", function (data) { _replacements = data; });
            _lastSystemCheck = data.lastSystemChange;
        }

        var lastLocalMessageId = $("#messages .message:last").attr("id");
        //		console.log("lastLocalMessageId: " + lastLocalMessageId);
        //		console.log("data.mostRecentMessage: " + data.mostRecentMessage);
        if (lastLocalMessageId != undefined && data.mostRecentMessage != null && lastLocalMessageId != data.mostRecentMessage) {
            if ($("#getNewestMessages.alert-link").length == 0)
                // trying to figure out why this shows up erroneously sometimes [jj 13Oct7]
                $("#messages").after(CreateAlert("<a href='#' id='getNewestMessages' data-lastlocalmessageid='" + lastLocalMessageId + "' data-mostrecentmessage='" + data.mostRecentMessage + "' data-roomid='" + _roomId + "' class='alert-link'>New messages</a> have been posted.", "success"));
        }

        // clear any duplicate timeouts and set this one up to fire again
        clearTimeout(_frequentInterval);
        if (!_windowFocused) _intervalDelay *= 2; // if the window is out of focus exponentially slow down the updater
        _frequentInterval = setTimeout(FrequentInterval, _intervalDelay);
    });
}

function UpdateNewMessageCount(data) {
    $("#roomList .list-group-item").each(function () {
        // console.log($(this).attr("id"), $(this).hasClass("forgotten"));
        var numNew = data.newMessages[$(this).attr("id")];
        $(this).find("span.badge").text(numNew > 0 && !$(this).hasClass("forgotten") ? numNew : "");
    });
}

// clear all messages, message previews, &c. and display a "Loading..." thing
function ResetPageDisplay() {
    _pageReady = false;
    UpdatePageTitle("Loading...");
    $("#roomDisplay .alert").not(".persist").remove(); // mostly to remove unwanted "new messages have been posted..."
    $("#roomDisplay").css("opacity", ".3");
    $("#moderatorControls").empty();
    CloseTopNav();
}

function UpdatePageTitle(title) {
    if (title == null) title = $("#pageTitle").html(); // this happens when we want to update the total new messages without changing the room name

    $("#pageTitle").html(title);
    $("head title").html(title + " - " + _systemName + (_totalNewMessages == 0 ? "" : " (" + _totalNewMessages + ")"));
}

// close the top nav whether in desktop display mode or mobile display mode
function CloseTopNav() {
    $("#userDropdown").parent().removeClass('open'); // hide dropdown in desktop view mode
    $(".navbar-collapse.in").removeClass('in').addClass("collapse"); // hide top nav when in mobile mode
}

// per http://stackoverflow.com/a/1145012 // there may be a problem with the jquery call on celina's chrome browser
function ScrollToTop() {
    // $("html, body").animate({ scrollTop: 0 }, "fast"); // on reload sometimes the browser wants to scroll down a ways; this prevents that
    window.scrollTo(0, 0);
}

// load the room and prepare everything for display
function LoadRoom(roomId, afterLoadCallback) {
    ResetPageDisplay();
    SetCurrentRoomId(roomId);

    ScrollToTop();
    $.get("/Room/GetRoom?roomId=" + roomId, function (data) {
        if (data.error == "noroom") { Goto(); return; }

        UpdatePageTitle(data.name);
        _canEdit = data.canEdit;
        if (data.isMail) $("#postForm .recipient").parent().show(); else $("#postForm .recipient").val("").parent().hide();
        if (data.isAnonymous) $("#postForm #anonymous").show(); else $("#postForm #anonymous").val("").hide();
        if (_canEdit) $("li#editRoom").show(); else $("li#editRoom").hide();

        if (data.isNSFW) {
            $("#markNSFW").hide();
            $("#markSFW").show();
        } else {
            $("#markNSFW").show();
            $("#markSFW").hide();
        }

        if (data.isForgotten) {
            $("#forgetRoom").hide();
            $("#unforgetRoom").show();
            $("#unforgetRoomAux").show();
        } else {
            $("#forgetRoom").show();
            $("#unforgetRoom").hide();
            $("#unforgetRoomAux").hide();
        }

        $("#postbox").val(data.autosavedMessage).data("prev", data.autosavedMessage).trigger("autosize.resize");

        $("#roomDisplay").trigger("roomloaded");

        if (afterLoadCallback != null) afterLoadCallback();
    });

    // unless ungoto // I now don't think this is necessary
    //	$.post("/Room/UpdateLastPointer?roomId=" + _roomId, function (data) { });

    $("#messagePreview").hide();
    $("#roomDisplay").css("opacity", "1");
    ResetPostbox();
    $("#messageButtons").show();
    FrequentInterval();
    ShowNew();
}

function SetCurrentRoomId(roomId) {
    $.cookie("roomId", roomId); // store the roomid in a cookie so we can restore this room on reload
    if (_roomId != null) {
        _ungoto.push(_roomId); // add the previous room to the ungoto list
        while (_ungoto.length > 50) _ungoto.shift(); // remove oldest rooms until the array is smaller
        // TODO: consider sticking the ungoto list in a browser cookie; then we can restore it on reload!
    }
    _roomId = roomId;
}

function ShowAll() {
    _pageReady = false;
    $("#messages").html("");
    $("#messagesLoading").show();
    $.get("/Room/GetAllMessages?roomId=" + _roomId, function (data) {
        $("#messagesLoading").hide();
        for (j = 0; j < data.length; j++) $("#messages").append(CreateMessageDom(data[j]));
        PlonkMessages();
        _pageReady = true;
        $("#messages").trigger("loaded");
    });
}

function ShowNew() {
    _pageReady = false;
    $("#messages").html("");
    $("#messagesLoading").show();
    $.get("/Room/GetNewMessages?roomId=" + _roomId, function (data) {
        $("#messagesLoading").hide();
        for (j = 0; j < data.length; j++) $("#messages").append(CreateMessageDom(data[j]));
        PlonkMessages();
        _pageReady = true;
        $("#messages").trigger("loaded");
    });
}

function ShowNewest() {
    $(".alert").alert("close"); // close all alerts? ok.
    $.get("/Room/GetNewestMessages?roomId=" + _roomId + "&newestMessageTicks=" + $("#messages .message:last").attr("ticks"), function (data) {
        for (j = 0; j < data.length; j++) $("#messages").append(CreateMessageDom(data[j]));
        PlonkMessages();
        $("#messages").trigger("loaded");
    });
}

function ShowMore() {
    // request the previous few messages
    var lastMessageTicks = $("#messages .message:first()").attr("ticks");

    _pageReady = false;
    $("#messagesLoading").show();
    $.get("/Room/GetMoreMessages?roomId=" + _roomId + "&lastMessageTicks=" + lastMessageTicks, function (data) {
        $("#messagesLoading").hide();
        // run the loop backwards because we're "prepending"
        for (j = data.length - 1; j >= 0; j--) $("#messages").prepend(CreateMessageDom(data[j]));
        PlonkMessages();
        _pageReady = true;
        $("#messages").trigger("loaded");
    });
}

function ShowToday() {
    _pageReady = false;
    $("#messages").html("");
    $("#messagesLoading").show();
    $.get("/Room/GetTodayMessages?roomId=" + _roomId, function (data) {
        $("#messagesLoading").hide();
        for (j = 0; j < data.length; j++) $("#messages").append(CreateMessageDom(data[j]));
        PlonkMessages();
        _pageReady = true;
        $("#messages").trigger("loaded");
    });
}

function PlonkMessages() {
    var $plonk = $("#plonkTemplate").clone().attr("id", "").css("display", "");
    $("#messages .plonk").not(".marked").before($plonk);
    $("#messages .plonk").not(".marked").addClass("marked");
}

// get the vote score and status for every displayed message
function GetVotes() {
    if (_roomId == _mailRoomId) return;

    // construct list of message ids (only if participating in voting)
    var messageIds = "";
    $("#messages .message").has(".messageControls .votingButtons.enabled").each(function () { messageIds += "," + $(this).attr("id"); });
    if (messageIds.length == 0) return;

    $.post("/Room/GetVotes", { messageIds: messageIds.substring(1) }, function (data) {
        // display messages scores
        for (messageId in data.scores) {
            $("#messages #" + messageId + ".message .score").text(data.scores[messageId]);

            var divisivness = data.counts[messageId] - Math.abs(data.scores[messageId]); // count - abs(score) // larger number is a more divisive post
            if (divisivness >= 3) $("#messages #" + messageId + ".message .divisive").show();
        }

        // show either the voting buttons or the unvote button depending on what this user has done
        $("#messages .message").each(function () {
            if ($(this).find(".author").attr("id") == _currentUser.id) return; // can't vote on own messages

            var messageId = $(this).attr("id");
            if (data.userVotes[messageId] == undefined) $(this).find(".votingButtons").show();
            else $(this).find(".unvote").show();
        });
    });
}

// given a JSON message object, return a message dom
function CreateMessageDom(msg) {
    var msgDom = $("#messageTemplate").clone().attr("id", msg.id).attr("ticks", msg.ticks).css("display", "");

    if (!msg.isNew) msgDom.find(".messageheader").addClass("text-muted");
    else msgDom.find(".messageheader").addClass("text-info");

    msgDom.find(".time").text(msg.date);
    msgDom.find(".author").attr("id", msg.authorId).text(msg.authorName);
    if (msg.recipientId != null) msgDom.find(".recipient").attr("id", msg.recipientId).text(msg.recipientName).parent().show();
    if (msg.originalRoomName != null) msgDom.find(".originalRoomName").text(msg.originalRoomName).parent().show();

    if (msg.isBirthday) msgDom.find(".birthday").show();
    if (msg.isIt) msgDom.find(".it").show();

    // disallow message move/delete from mail
    if (_roomId == _mailRoomId || !_canEdit) msgDom.find("input.messageSelector").hide();

    // show "reply" button
    if (_roomId == _mailRoomId && msg.authorId != _currentUser.id) msgDom.find(".reply").show();

    // inline body content formatting
    var body = ReplaceYouTubeLinks(msg.body);
    body = QuickFormat(body); // convert EOLs to BRs, italicize quotes lines, &c.
    body = ActivateCutTags(body);
    body = ReplaceURLWithHTMLLinks(body);
    for (var i in _replacements) body = body.replaceAll(i, _replacements[i]); // arbitrary string replacement from /Content/replacements.json
    msgDom.find(".body").html(body);

    // attachments
    for (attIndex = 0; attIndex < msg.attachments.length; attIndex++) {
        var attachmentDom = "";
        if (msg.attachments[attIndex].isImage) {
            switch (_currentUser.attachmentDisplay) {
                case "Thumbnail":
                    attachmentDom = "<a href='Res/" + msg.attachments[attIndex].filename + "' target='_blank'><img src='Res/" + msg.attachments[attIndex].filename + "?width=120' alt='" + msg.attachments[attIndex].filename + "' class='thumbnail' /></a>";
                    break;
                case "Full":
                    attachmentDom = "<a href='Res/" + msg.attachments[attIndex].filename + "' target='_blank'><img src='Res/" + msg.attachments[attIndex].filename + "' alt='" + msg.attachments[attIndex].filename + "' class='img-responsive' /></a>";
                    break;
                case "Half":
                    attachmentDom = "<a href='Res/" + msg.attachments[attIndex].filename + "' target='_blank'><img src='Res/" + msg.attachments[attIndex].filename + "' alt='" + msg.attachments[attIndex].filename + "' style='width: 50%;' class='img-responsive' /></a>";
                    break;
                case "Text":
                    attachmentDom = "<a href='Res/" + msg.attachments[attIndex].filename + "' target='_blank'><button type='button' class='btn-sm btn btn-default'>" + msg.attachments[attIndex].filename + "</button></a>";
                    break;
            }
        }
        else attachmentDom = "<a href='Res/" + msg.attachments[attIndex].filename + "' target='_blank'><button type='button' class='btn-sm btn btn-default'>" + msg.attachments[attIndex].filename + "</button></a>";
        msgDom.find(".attachments").append(attachmentDom);
    }

    if ($.inArray(msg.authorId, _plonks) >= 0) {
        msgDom.addClass("plonk");
    }

    // 4/1/25
    if ((new Date()).getMonth() == 3 && (new Date()).getDate() == 1) {
        if (Math.random() < .05) {
            const ads = ["altavista-2000.gif", "amazon-2000.gif", "apple-1997.jpg", "at-t-the-first-banner-1994.png", "buy-com-2000.gif", "buy-me-a-coffee.png", "cdnow-1998.gif", "coolsavings-1999.gif", "dice-2000.gif", "ebay-2000.gif", "elle-2000.gif", "excite-2000.gif", "frontpage-1996.gif", "get-flash-player-1996.gif", "hotmail-2000.gif", "hp-shopping-2000.gif", "ibm-1996.jpg", "ibm-1997.gif", "internet-explorer-1996.gif", "internet-explorer-2000.gif", "internet-study-1998.gif", "lowest-fare-1999.gif", "lycos-autos-2000.gif", "mac-mall-1998.gif", "macromedia-2000.gif", "macromedia-flash-3-1998.gif", "match-com-2000.gif", "microsoft-1999.gif", "microsoft-backoffice-1996.gif", "msn-1999.jpg", "ncaa-march-madness-1999.gif", "netscape-1995.gif", "netscape-1996.gif", "netscape-2000.gif", "netscape-netcenter-personal-finance-1999.gif", "nike-2000.gif", "panasonic-1998.gif", "patreon.png", "pentium-1999.gif", "pizza-hut-1998.gif", "postnet-1999.gif", "quicktime-4-0-2000.gif", "real-player-g2-2000.gif", "space-network-1998.gif", "the-street-2000.gif", "thomas-regional-1998.gif", "visa-1999.gif", "volkswagen-2000.gif", "windows-95-1996.gif", "winzip-8-0-2000.gif", "yahoo-pager-1998.gif"];
            var unnecessaryContainer = document.createElement("div");
            unnecessaryContainer.innerHTML = msgDom[0].outerHTML + `<p class="text-center" style="margin: 50px 0;"><img style="max-width: 100%;" src="/images/ads/${ads[parseInt(Math.random() * ads.length)]}"></p>`;
            return unnecessaryContainer;
        }
    }

    return msgDom;
}

// replace youtube links that appear on their own line
function ReplaceYouTubeLinks(text) {
    var replacement = "$1 <span class='glyphicon glyphicon-expand inlineyoutube' style='font-size: 1.4em; cursor: pointer;'></span> <iframe style='display: none; margin: 10px 0 10px 0;' width=\"960\" height=\"720\" src='http://www.youtube.com/embed/$2?rel=0&wmode=transparent&autoplay=0&$3' frameborder=\"0\" allowfullscreen></iframe>";

    // actual url format: http://www.youtube.com/watch?v=ScA7vDC2u10&redirect=0
    text = text.replace(new RegExp("^(https*:\\/\\/www.youtube.com\\/watch\\?v\\=([a-z0-9_-]*)\\&*(.*)$)", "gim"), replacement);

    // "share" url: http://youtu.be/ScA7vDC2u10?t=2
    text = text.replace(new RegExp("^(https*:\\/\\/youtu.be\\/([a-z0-9_-]*)\\?*(.*)$)", "gim"), replacement);

    // replace "t=2s" in iframe src with "start=2" (because that's just how it works) [jj 13Oct20]
    text = text.replace(new RegExp("iframe(.*)t\\=([0-9]+)s", "gim"), "iframe$1start=$2");

    return text;
}

function QuickFormat(text) {
    if (text == undefined) return "";

    // auto-italicize lines beginning with '>'
    var regex = /^\>(.*)$/igm;
    text = text.replace(regex, "&gt;<i>$1</i>");

    /* deprecated. we'll just use the "pre" tag for this sort of thing [jj 13Oct20]
    // lines that begin with a space should display as fixed width
    // first escape apostrophes (because we stick this in a html property and apostrophes are delimiters)
    var originalText = "";
    while (originalText != text) { // we have to loop this because thre might be multiple apos in one line and this regex can only do one at a time
    originalText = text;
    text = text.replace(new RegExp("^( .*?)(\')", "gm"), "$1&#39;"); //  &#39;
    }

    regex = /^ (.*)$/igm;
    text = text.replace(regex, "<span class='code' content='$1'></span>");
    */

    // hide contents of pre tags to avoid further processing
    var replacements = {};
    var code_regex = new RegExp("<pre>([\\s\\S]*?)\\</pre>", "gi"); // we use [\s\S] instead of . because javascript regex doesn't support "singleline" mode and we need to include EOLs
    var code_matches = text.match(code_regex);
    if (code_matches != null) {
        for (i = 0; i < code_matches.length; i++) {
            var id = Math.random();
            replacements[id] = code_matches[i];
            text = text.replace(code_matches[i], id);
        }
    }

    // convert EOLs to <br>s - this has to happen before we convert the text to a dom or we lose all whitespace // cross-platform technique [jj 13Sep11]
    text = text.replace(new RegExp("<br>\r\n", "g"), "\r\n"); // test
    text = text.replace(new RegExp("\n", "g"), "<br>"); // changed "\r\n" to just "\n", that seems to be more reliable (at least with profile text)

    var domRep = $("<div>" + text + "</div>");
    /* span.code deprecated [jj 13Oct20]
    domRep.find("span.code").each(function () {
    $(this).text($(this).attr("content")).removeAttr("content"); // writing the content to the text method automatically escapes all HTML characters
    });
    */

    domRep.find("img").not(".skip").each(function () {
        switch (_currentUser.attachmentDisplay) {
            case "Full": $(this).removeAttr("style").removeAttr("class").attr("class", "img-responsive"); break;
            case "Half": $(this).removeAttr("style").removeAttr("class").attr("class", "img-responsive").css("width", "50%"); break;
            case "Thumbnail": $(this).replaceWith("<a href='" + $(this).attr("src") + "' target='_blank'><img src='" + $(this).attr("src") + "' class='thumbnail' /></a>"); break;
            case "Text": $(this).replaceWith("<a href='" + $(this).attr("src") + "' target='_blank'><button type='button' class='btn-sm btn btn-default'>" + $(this).attr("src") + "</button></a>"); break;
        }
    });

    // check every iframe src attribute; if it contains youtube.com but not "wmode=transparent" then add it // [jj 13Nov7]
    domRep.find("iframe").each(function () {
        var src = $(this).attr("src")
        // console.log(src);

        if (new RegExp("youtube\\.com").test(src) && !(new RegExp("wmode=transparent").test(src))) {
            // console.log("found and needing");
            if (new RegExp("\\?").test(src)) src += "&wmode=transparent";
            else src += "?wmode=transparent";
            $(this).attr("src", src);
        }
    });

    // reverse earlier replacements
    text = domRep.html();
    for (id in replacements) text = text.replace(id, replacements[id]);

    return text;
}

function ActivateCutTags(text) {
    var domRep = $("<div>" + text + "</div>");
    domRep.find(".cut").each(function () {
        $(this).before("<a href='#' class='revealCut'>" + $(this).attr("title") + "</a>");
        if ($(this).html().substr(1, 4) == "<br>") $(this).html($(this).html().substring(5)); // remove the first <br>; it's just extra spacing
    });
    return domRep.html();
}

// derived from http://stackoverflow.com/a/37687
function ReplaceURLWithHTMLLinks(text) {
    var contentDict = {};
    var hrefDict = {};
    var srcDict = {};

    // convert the source text to a jQuery dom node
    var domRep = $("<div>" + text + "</div>");

    // obfuscate anchor tags
    domRep.find("a").each(function () {
        // store the element content in the local dictionary
        var id = Math.random();
        contentDict[id] = $(this).html();
        hrefDict[id] = $(this).attr("href");

        //$(this).html(id).attr("href", $(this).attr("href").replace("http", "ht_tp")).attr("target", "_blank");
        $(this).html(id).removeAttr("href").attr("target", "_blank");
    });

    // and img & iframe tags
    domRep.find("img,iframe").each(function () {
        var id = Math.random();
        srcDict[id] = $(this).attr("src");
        // $(this).attr("src", $(this).attr("src").replace("http", "ht_tp"));
        // $(this).removeAttr("src").data("src", id); // can't user .data() because it gets wiped out by the call to domRep.html() [jj 13Oct25]
        $(this).attr("xsrc", id).removeAttr("src");
    });

    // now auto-link remaining urls
    // var exp = /(\b(https?):\/\/[-A-Z0-9+&@#\/%?=~_|!:,.;]*[-A-Z0-9+&@#\/%=~_|])/ig; // removed "|ftp|file" from regex; we don't really support those
    var exp = /(\b(https?):\/\/[-A-Z0-9+&@#\/%?=~_|!:,.;()]*[-A-Z0-9+&@#\/%=~_|)])/ig; // added parens [jj 13Dec14]
    // ideally, we would only search text here, instead of everything (eg, attributes of tags).
    // I wonder if it would be possible to separate dom elements from text elements?
    domRep.html(domRep.html().replace(exp, "<a href='$1' target='_blank'>$1</a>"));

    // restore anchor tag contents
    domRep.find("a").each(function () {
        var id = $(this).html();
        $(this).html(contentDict[id]).attr("href", hrefDict[id]);
    });

    // and restore img & iframe tags
    domRep.find("img,iframe").each(function () {
        var id = $(this).attr("xsrc");
        $(this).attr("src", srcDict[id]).removeAttr("xsrc");
    });

    domRep.find("a").each(function () {
        var href = $(this).attr("href")

        const textIsSame = href == $(this).text();

        // facebook warning dialog
        if (href != undefined && (href.indexOf("facebook.com") >= 0 || href.indexOf("fb.me") >= 0)) {
            $(this).attr("href", "#").addClass("fb-link");
            $(this).attr("original", href);
        }

        // remove tracking from amazon urls
        if (href != undefined && href.indexOf("amazon.com") >= 0) {
            href = href.replace(new RegExp("/ref=(.*)", "g"), "");
            $(this).attr("href", href);
        }

        // remove utm_* trackers
        const utmRegEx = new RegExp("utm_.+?=[^&]+", "g");
        if (href != undefined && href.indexOf("utm_") >= 0) {
            href = href.replaceAll(utmRegEx, "x");
            $(this).attr("href", href);
        }

        // remove bt_* trackers
        if (href != undefined && href.indexOf("bt_") >= 0) {
            href = href.replace(new RegExp("bt_.+?=[^&]+", "g"), "x");
            $(this).attr("href", href);
        }

        if (textIsSame) $(this).text(href);
    });

    // add 12ft and archive.is alt links
    domRep.find("a:not(.revealCut)").wrap("<div class='linkContainer'></div>");
    domRep.find("a:not(.revealCut)").after(function () {
        return `<span><a class="alt" target="_blank" href="https://12ft.io/${this.href}">12ft</a> <a class="alt" target="_blank" href="https://archive.is/${this.href}">archive.is</a></span>`;
    });

    // finally de-obfuscate the previously obfuscated urls
    var body = domRep.html();
    // body = body.replace(new RegExp("ht_tp", "g"), "http");
    return body;
}