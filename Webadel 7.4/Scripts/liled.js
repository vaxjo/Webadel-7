
$(document).ready(function () {

    $("#roomDisplay").on("click", ".liled", function () {
        var let = $(this).data("let");

        $.post("/LilEd/Found", { let: let }, function (data) {
            alert(data);
        });

        $(this).removeClass("liled").data("let", null);
    });

});
