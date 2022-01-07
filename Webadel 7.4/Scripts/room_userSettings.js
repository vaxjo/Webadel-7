$(document).ready(function () {
    $("#changePwModal").on('show.bs.modal', function () { CloseTopNav(); });
    $("#changePwModal").on('shown.bs.modal', function () { $("#currentPassword").focus(); });
    $("#changePwModal #changePw").click(function () { ChangePassword(); });

    $("#sortRoomsModal").on('show.bs.modal', function () { CloseTopNav(); SortRoomList(); });
    $("#sortRoomsModal").on("click", "#finished", function () { SortRoomList_Finished(); });

    $("#profileModal").on('show.bs.modal', function () { CloseTopNav(); Profile(); });
    $("#profileModal").on('shown.bs.modal', function () { $("#email").focus(); });
    $("#profileModal").on("click", "#save", function () { Profile_Finished(); });

    // this is the link that shows up in the "new user edit-your-profile" alert [jj 20Aug12]
    $("#editProfile_alert").click(function () {
        $(this).closest(".alert").remove();
        $("#profileModal").modal("show");
    });

    $("#userSettingsModal").on('show.bs.modal', function () { CloseTopNav(); AccountSettings(); });
    $("#userSettingsModal").on('shown.bs.modal', function () { $("#email").focus(); });
    $("#userSettingsModal").on("change", "#theme", function () { ChangeTheme($(this).val()); });
    $("#userSettingsModal").on("click", "#save", function () { AccountSettings_Finished(); });

    $("#plonkModal").on('show.bs.modal', function () { CloseTopNav(); PlonkSettings(); });
    $("#plonkModal").on("click", ".unplonk", function () { UnplonkUser($(this).data("id")); });
    $("#plonkModal").on("change", "#plonkUser", function () { PlonkUser($(this).val()); });
    $("#plonkModal").on("click", "#save", function () { $("#plonkModal").modal("hide"); });
    $("#plonkModal").on('hidden.bs.modal', function () { window.location.reload(); });
});

function AccountSettings() {
    $("#userSettingsModal").load("/Room/Index_AccountSettings_Dialog", function () {
        $("#userSettingsModal #theme").val($.cookie("webadelTheme"));
    });
}

function ChangeTheme(theme) {
    $("#linkTheme").attr("href", "/Content/themes/" + theme + ".min.css");
    $.cookie("webadelTheme", theme, { expires: 9999 });
}

function AccountSettings_Finished() {
    $.post("/Room/AccountSettings", $("#userSettingsModal form").serialize(), function () {
        $("#userSettingsModal").modal("hide");
        // some property changes require a reload
        if ($("#userSettingsModal #enableSwipe").data("original") != $("#userSettingsModal #enableSwipe").val() ||
            $("#userSettingsModal #enablePredictiveText").data("original") != $("#userSettingsModal #enablePredictiveText").val() ||
			$("#userSettingsModal #username").data("original") != $("#userSettingsModal #username").val() ||
			$("#userSettingsModal #attachmentDisplay").data("original") != $("#userSettingsModal #attachmentDisplay").val()) window.location.reload();
    });
}

function PlonkSettings() {
    $("#plonkModal").load("/Room/Index_PlonkSettings_Dialog", function () { PlonkSettings_LoadForm(); });
}

function PlonkSettings_LoadForm() {
    $("#plonkModal .modal-body").load("/Room/Index_PlonkSettings_Form", function () { });
}

function PlonkUser(id) {
    $.post("/Room/PlonkUser?userId=" + id, function () { PlonkSettings_LoadForm(); });
}

function UnplonkUser(id) {
    $.post("/Room/UnplonkUser?userId=" + id, function () { PlonkSettings_LoadForm(); });
}

function Profile() {
    $("#profileModal").load("/Room/Index_Profile_Dialog", function () { });
}
function Profile_Finished() {
    $.post("/Room/UpdateProfile", $("#profileModal form").serialize(), function () {
        $("#profileModal").modal("hide");
    });
}

function SortRoomList() {
    $("#sortRoomsModal").load("/Room/Index_SortRooms_Dialog", function () {
        $("#sortRoomsModal ul").sortable({ axis: "y", tolerance: 'pointer', revert: false, forceHelperSize: true });
    });
}
function SortRoomList_Finished() {
    var roomsIds = "";
    $("#sortRoomsModal ul li").each(function () { roomsIds += $(this).attr("id") + ","; });

    $.post("/Room/ReorderRooms?roomIds=" + roomsIds, function () {
        $("#sortRoomsModal").modal("hide");
        FrequentInterval(); // reload room lists
    });
}

function ChangePassword() {
    $.post("/Room/ChangePassword", $("#changePwModal form").serialize(), function (data) {
        if (data == "") {
            $("#changePwModal").modal("hide");
            $("#messageButtons").prepend(CreateAlert("Your password has been successfully changed.", "success"));
        }

        $("#changePwModal .form-group").removeClass("has-error");
        $("#changePwModal .alert").remove();
        if (data.indexOf("passwordwrong") >= 0) {
            $("#changePwModal #currentPassword").parent().addClass("has-error").prepend(CreateAlert("Password is incorrect.", "warning"));
        }
        if (data.indexOf("currentpasswordempty") >= 0) {
            $("#changePwModal #currentPassword").parent().addClass("has-error").prepend(CreateAlert("You must enter a password.", "warning"));
        }
        if (data.indexOf("newpasswordempty") >= 0) {
            $("#changePwModal #newPassword").parent().addClass("has-error").prepend(CreateAlert("You must enter a password.", "warning"));
        }
        if (data.indexOf("badpw") >= 0) {
            $("#changePwModal #newPassword").parent().addClass("has-error").prepend(CreateAlert("You must choose a better password.", "warning"));
        }
        if (data.indexOf("pwisusername") >= 0) {
            $("#changePwModal #newPassword").parent().addClass("has-error").prepend(CreateAlert("Your password cannot match your username.", "warning"));
        }
        if (data.indexOf("confirm") >= 0) {
            $("#changePwModal #confirmPassword").parent().addClass("has-error").prepend(CreateAlert("Your passwords must match.", "warning"));
        }
    });
}
