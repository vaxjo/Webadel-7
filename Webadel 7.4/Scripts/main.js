var _authToken; // maybe not important?
var _textareaHasFocus = false; 	// a boolean to let us know if any textarea currently has focus

$(document).ready(function () {
    // global webadel script

    // the only reliable way to know if a textarea currently has focus
    $("body").on("focus", "textarea", function () { _textareaHasFocus = true; });
    $("body").on("blur", "textarea", function () { _textareaHasFocus = false; });

    setTimeout(function () { $("#welcomeback").fadeOut(); }, 5000);

    $(document).ajaxError(function (event, jqxhr, settings, exception) {
        //  console.log("jqxhr.status: " + jqxhr.status);

        if (jqxhr.status == "0") return; // this happens when I load a new web site, not sure why

        if (jqxhr.status == "404") return; // this happens a lot on reload and I don't think it's important enough to pop a modal
        //		console.log(event);
        //		console.log(jqxhr);
        //		console.log(settings);
        //		console.log(exception);

        // this hits when the server is not available (server down, whatever)
        $("#errorModal #errorStatus").text(jqxhr.status);
        $("#errorModal #errorStatusText").text(jqxhr.statusText);
        $("#errorModal").modal("show");
        if (typeof _frequentInterval != "undefined") clearTimeout(_frequentInterval); // stop the frequentInterval timeout
    });

    Mousetrap.bind(". m", function (e) { if (!_textareaHasFocus) window.location = "https://www.youtube.com/watch?v=wrw1VMRNFUg"; });
});

var _months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
function CitDate(datetime) {
    var h = datetime.getHours();
    var tt = "am";
    if (h == 0) { h = 12; }
    else if (h == 12) { tt = "pm"; }
    else if (h > 12) {
        h -= 12;
        tt = "pm";
    }

    var datePart = _months[datetime.getMonth()] + datetime.getDate();

    if (datePart == "Nov1") datePart = "Oct32"; // extend halloween by a day

    datePart = datetime.getFullYear().toString().substr(2, 2) + datePart;

    var timePart = h + ":" + (datetime.getMinutes() < 10 ? "0" : "") + datetime.getMinutes() + " " + tt;

    return datePart + " " + timePart;
}

// type can be success, info, warning or danger
function CreateAlert(alertText, type, autoFade) {
    var id = "alert_" + Math.random().toString().substring(2);

    if (autoFade) setTimeout(function () { $("#" + id).alert("close"); }, 4000);

    return $("<div id='" + id + "' class='alert alert-dismissable fade in alert-" + type + "'><button type='button' class='close' data-dismiss='alert' aria-hidden='true'>&times;</button>" + alertText + "</div>");
}

function OpenModal(url, finished) {
    $("#modal").css("cursor", "").html($("#loadingModal").clone().show()).modal("show");
    $("#modal").load(url, function (responseText, textStatus, jqXHR) {
        //console.log(responseText);
        //console.log(textStatus);
        //console.log(jqXHR);
        if (textStatus == "error") {
            $("#modal").modal("hide");
            alert("An error occurred.");
        }

        if ($.isFunction(finished)) finished();
    });
}

function CloseModal() {
    $("#modal").modal("hide");
}

// close a modal that is open and, when it's finished closing, call the onFinished fn
function CloseModal($openModal, onFinished) {
    if ($openModal.length == 0) { // no modal currently open so just show it
        onFinished();
        return;
    }

    // watch for the hidden event
    $openModal.on("hidden.bs.modal", function () {
        $openModal.off("hidden.bs.modal"); // also unwatch so they don't stack up
        onFinished();

    }).modal("hide"); // trigger the hiding sequence
}