$(document).ajaxStart(function () {
    $('#AjaxLoader').show();
})

.ajaxStop(function () {
    $('#AjaxLoader').hide();
})