$(document).ready(function () {
    $("#messages").on("mouseenter", ".messageControls", function () {
        $(this).animate({ opacity: 1 }, "fast");

    }).on("mouseleave", ".messageControls", function () {
        if ($(this).find(".messageSelector").prop("checked")) return; // don't reduce the opacity if the checkbox is checked
        $(this).animate({ opacity: .3 }, "fast");

    }).on("click", ".undo", function () { // user wants to delete their own message
        var $message = $(this).parents(".message");
        $.post("/Messages/DeleteLastMessage?messageId=" + $message.attr("id"), function (r) {
            if (r[0] == 0) {
                $("#postbox").val(r.substring(1));
                autosize.update($("#postForm #postbox"));
                $message.remove();
            } else {
                alert("Message can no longer be removed.");
            }
        });

    }).on("click", ".messageSelector", function () {
        // if ($(this).prop("checked")) $(this).parent().parent().addClass("selected"); else $(this).parent().parent().removeClass("selected");
        if ($("#messages .messageSelector:checked").length == 0) $("#moderatorControls").hide();
        else {
            // if ($("#moderatorControls form").length == 0) // no, let's load it every time
            $("#moderatorControls").load("/Room/Index_ModeratorControls");
            $("#moderatorControls").show();
        }

    }).on("click", ".score", function () { // get and display the users who have voted on this message 
        var voteScore = $(this);
        $.get("/Room/GetVoters?messageId=" + $(this).closest(".message").attr("id"), function (data) {
            if (data == "") return; // non-cosysops always get an empty result
            voteScore.popover({ content: data, html: true }).popover("show");
        });

    }).on("click", ".vote", function () {
        var msgDom = $(this).parents(".message");
        $.getJSON("/Room/Vote?messageId=" + msgDom.attr("id") + "&vote=" + $(this).data("vote"), function (data) {
            msgDom.find(".votingButtons").hide();
            msgDom.find(".unvote").show();
            msgDom.find(".score").text(data == null ? "" : data);
        });

    }).on("click", ".unvote", function () {
        var msgDom = $(this).parents(".message");
        $.getJSON("/Room/Vote?messageId=" + msgDom.attr("id") + "&vote=None", function (data) {
            msgDom.find(".votingButtons").show();
            msgDom.find(".unvote").hide();
            msgDom.find(".score").text(data == null ? "" : data);
        });
    });

    $("#moderatorControls").on("change", "#moveRoomId", function () {
        var nameField = $("#moderatorControls #newRoomName").parent();
        if ($(this).val() == "new") nameField.show(); else nameField.hide();

    }).on("click", "#move", function () {
        var selectedMessageIds = "";
        $("#messages .messageSelector:checked").each(function () { selectedMessageIds += $(this).parent().parent().attr("id") + ","; });
        // console.log(selectedMessageIds);

        // the "new" value is only used to show/hide the "new room name" field; now we need it to be empty
        if ($("#moderatorControls #moveRoomId").val() == "new") $("#moderatorControls #moveRoomId").val("");
        $.post("/Room/MoveMessages?moveRoomId=" + $("#moderatorControls #moveRoomId").val() + "&newRoomName=" + $("#moderatorControls #newRoomName").val() + "&selectedMessages=" + selectedMessageIds, function (data) {
            if (data != "") { alert(data); return; }
            $("#moderatorControls").hide();
            $("#messages .messageSelector:checked").parents(".message").remove();
        });
    });
});