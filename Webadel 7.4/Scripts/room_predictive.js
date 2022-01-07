$(document).ready(function () {

    // on every keyup ask the server for a set of predictive words
    $("#postForm #postbox").keyup(function () { ParseText(); });

    // add the selected word to the textbox
    $("#predictive button").click(function () {
        if ($(this).data("word") == null) return;

        $("#postForm #postbox").val($("#postForm #postbox").val() + " " + $(this).data("word")); ParseText();
    });
});

function ParseText() {
    if (!_currentUser.enablePredictiveText) return;

    var text = $("#postForm #postbox").val();
    //console.log(text);
    var lwreg = new RegExp("\\s*(\\S*)\\s*$", "g");
    var lastword = text.split(lwreg);
    //console.log("lastword", lastword);

    $.get("/Predict?lastword=" + lastword[1], function (words) {
        //console.log(words);
        $("#predictive button").data("word", null).html("&nbsp;");
        for (var i = 0; i < words.length; i++) {
            $("#predictive button:eq(" + i + ")").data("word", words[i]).html(words[i]);
        }
        $("#predictive").show();
    });
}