﻿var _ungoto = new Array(); 		// list of visited rooms

$(document).ready(function () {
    $("#createRoomModal").on('show.bs.modal', function () { CreateRoom(); });
    $("#createRoomModal").on('shown.bs.modal', function () { $("#name").focus(); });
    $("#createRoomModal").on("click", "#save", function () { CreateRoom_Finished(); });
    $("#createRoomModal").on("click", "#private", function () { $("#createRoomModal #invitees").parent().toggle(); });

    $("#roomInfoModal").on('show.bs.modal', function () {
        CloseTopNav();
        $("#roomInfoModal").load("/Room/Index_RoomInfo_Dialog?roomId=" + _roomId, function () { });
    });

    $("#editRoomModal").on('show.bs.modal', function () { EditRoom(); });
    $("#editRoomModal").on('shown.bs.modal', function () { $("#name").focus(); });
    $("#editRoomModal").on("click", "#save", function () { EditRoom_Finished(); });
    $("#editRoomModal").on("click", "#delete", function () { EditRoom_Delete(); });
    $("#editRoomModal").on("click", "#private", function () { $("#editRoomModal #invitees").parent().toggle(); });

    $("#searchModal").on('show.bs.modal', function () {
        CloseTopNav();
        $("#searchModal").html("").load("/Room/Index_Search_Dialog", function () { });
    });
    $("#searchModal").on('shown.bs.modal', function () { $("#searchModal #q").focus(); });
    $("#searchModal").on("click", "#search", function () { Search(); });
    $("#searchModal").on("keypress", "input, select", function (event) {
        if (event.keyCode == 13) {
            Search();
            return false;
        }
    });
    $("#messages").on("click", ".roomLink", function () { LoadRoom($(this).attr("id")); });

    $("#markNSFW").click(function () { if (_pageReady) MarkNSFW(); });
    $("#markSFW").click(function () { if (_pageReady) MarkSFW(); });
    $("#forgetRoom").click(function () { if (_pageReady) ForgetRoom(); });
    $("#unforgetRoom").click(function () { if (_pageReady) UnforgetRoom(); });
    $("#unforgetRoomAux").click(function () { $("#unforgetRoom").click(); $("#unforgetRoomAux").hide(); });
    $("#messages").on("click", ".forgetExpiration button", function () { if (_pageReady) ExpireForget($(this).data("timeout")); });

    $("#goto").click(function () { if (_pageReady) Goto(); });
    $("#skip").click(function () { if (_pageReady) Skip(); });
    $("#jump").on("click", "a", function () { LoadRoom($(this).attr("id")); });
    $("#ungoto").click(function () { if (_pageReady) Ungoto(); });

    // mobile control buttons
    $("#mobileGoto").click(function () { if (_pageReady) Goto(); });
    $("#mobileSkip").click(function () { if (_pageReady) Skip(); });
    $("#mobileUngoto").click(function () { if (_pageReady) Ungoto(); });
});

function Goto() {
    ResetPageDisplay();
    var pointer = $("#messages .message:last()").attr("ticks");
    $.post("/Room/SetPointer?roomId=" + _roomId + "&ticks=" + pointer, function (data) {
        $.get("/Room/GetNextRoom?roomId=" + _roomId, function (data) { LoadRoom(data); });
    });
}

function Skip() {
    ResetPageDisplay();
    $.get("/Room/GetNextRoom?roomId=" + _roomId, function (data) { LoadRoom(data); });
}

function Ungoto() {
    if (_ungoto.length == 0) return;

    var ungotoRoomId = _ungoto.pop();
    $.post("/Room/RestoreLastPointer?roomId=" + ungotoRoomId, function (data) {
        LoadRoom(ungotoRoomId, function () {
            _ungoto.pop(); // pop and remove the newly added room id; if we ungoto again we want to continue back the way we came
        });
    });
}

function CreateRoom() {
    CloseTopNav();
    $("#createRoomModal").html("").load("/Room/Index_Room_Dialog", function () { });
}
function CreateRoom_Finished() {
    $("#createRoomModal .modal-footer").html("<div class=\"progress progress-striped active\"><div class=\"progress-bar\" role=\"progressbar\" aria-valuenow=\"100\" aria-valuemin=\"0\" aria-valuemax=\"100\" style=\"width: 100%;\"></div></div>");
    $.post("/Room/UpdateRoom", $("#createRoomModal form").serialize(), function (data) {
        $("#createRoomModal").modal("hide");
        LoadRoom(data);
    });
}

function EditRoom() {
    CloseTopNav();
    $("#editRoomModal").html("").load("/Room/Index_Room_Dialog?roomId=" + _roomId, function () { });
}
function EditRoom_Finished() {
    $("#editRoomModal .modal-footer").html("<div class=\"progress progress-striped active\"><div class=\"progress-bar\" role=\"progressbar\" aria-valuenow=\"100\" aria-valuemin=\"0\" aria-valuemax=\"100\" style=\"width: 100%;\"></div></div>");
    $.post("/Room/UpdateRoom", $("#editRoomModal form").serialize(), function () {
        $("#editRoomModal").modal("hide");
        LoadRoom(_roomId);
    });
}
function EditRoom_Delete() {
    if (!confirm("Are you sure you want to irredemably delete this room?")) return;

    $("#editRoomModal .modal-footer").html("<div class=\"progress progress-striped active\"><div class=\"progress-bar\" role=\"progressbar\" aria-valuenow=\"100\" aria-valuemin=\"0\" aria-valuemax=\"100\" style=\"width: 100%;\"></div></div>");
    $.post("/Room/DeleteRoom", $("#editRoomModal form").serialize(), function () {
        SetCurrentRoomId(null);
        $("#editRoomModal").modal("hide");
        Goto();
    });
}

function ForgetRoom() {
    CloseTopNav();
    if (_roomId == null) return;
    $.post("/Room/Forget?forget=true&roomId=" + _roomId, function (data) {
        $("#messages").prepend(data);
        $("#forgetRoom").hide();
        $("#unforgetRoom").show();
        FrequentInterval();
    });
}
function UnforgetRoom() {
    CloseTopNav();
    if (_roomId == null) return;
    $.post("/Room/Forget?forget=false&roomId=" + _roomId, function (data) {
        $("#messages").prepend(data);
        $("#forgetRoom").show();
        $("#unforgetRoom").hide();
        FrequentInterval();
    });
}
function ExpireForget(timeout) {
    CloseTopNav();
    if (_roomId == null) return;

    $.post("/Room/ExpireForget?timeout=" + timeout + "&roomId=" + _roomId, function (data) {
        $("#messages").prepend(data);
    });
}
function MarkNSFW() {
    CloseTopNav();
    if (_roomId == null) return;
    $.post("/Room/NSFW?nsfw=true&roomId=" + _roomId, function (data) {
        $("#messages").prepend(CreateAlert("This room is now marked as NSFW. It will no longer show up in your Goto stream provided you are logged in from 'work'.", "info", true));
        $("#markNSFW").hide();
        $("#markSFW").show();
        FrequentInterval();
    });
}
function MarkSFW() {
    CloseTopNav();
    if (_roomId == null) return;
    $.post("/Room/NSFW?nsfw=false&roomId=" + _roomId, function (data) {
        $("#messages").prepend(CreateAlert("This room is no longer marked as NSFW.", "info", true));
        $("#markNSFW").show();
        $("#markSFW").hide();
        FrequentInterval();
    });
}

function Search() {
    _pageReady = false;
    $("#searchModal .alert").remove();
    $("#messagesLoading").show();

    if ($("#searchModal input[name='global']:checked").val() == "False") {
        $.get("/Room/Search?roomId=" + _roomId + "&userId=" + $("#searchModal #userId").val() + "&q=" + escape($("#searchModal #q").val()), function (data) {
            $("#messagesLoading").hide();
            if (data.length == 0) {
                $("#searchModal form").prepend(CreateAlert("No messages found that matched your search criteria.", "info"));
            } else {
                $("#searchModal").modal("hide");
                $("#messages").html("");
                for (i = 0; i < data.length; i++) $("#messages").append(CreateMessageDom(data[i]));
                _pageReady = true;
                GetVotes();
            }
        });

    } else {
        // global search
        $.get("/Room/Search?userId=" + $("#searchModal #userId").val() + "&q=" + escape($("#searchModal #q").val()), function (data) {
            $("#messagesLoading").hide();
            if (data.length == 0) {
                $("#messages").html("");
                $("#searchModal form").prepend(CreateAlert("No messages found that matched your search criteria.", "info"));
                _pageReady = true;
            } else {
                // we're not really in a "room" anymore so we have to obscure a bunch of stuff
                UpdatePageTitle("Global Search");
                $("#postForm").hide();
                $("#messageButtons").hide();
                SetCurrentRoomId(null);
                _canEdit = false;
                $("#searchModal").modal("hide");
                $("#messages").html("<h3>Found " + data.length + " messages...</h3>");
                var previousRoom = "";
                for (i = 0; i < data.length; i++) {
                    if (data[i].roomName != previousRoom) {
                        $("#messages").append("<h4 class='roomLink' id='" + data[i].roomId + "'>" + data[i].roomName + " &gt;</span></h4>");
                        previousRoom = data[i].roomName;
                    }
                    $("#messages").append(CreateMessageDom(data[i]));
                }
                _pageReady = true;
                GetVotes();
            }
        });
    }
}