
$(document).ready(function () {

    $("body").on("click", ".microbadge", function () {
        var id = $(this).data("id");

        var $openModal = $(".modal:visible");

        CloseModal($openModal, function () {
            //$("#badgeModal").load("/Badges/Badge_Modal?id=" + id, function () {
            //    $("#badgeModal").modal("show");
            //});

            OpenModal("/Badges/Badge_Modal?id=" + id);
        });
    });
});
