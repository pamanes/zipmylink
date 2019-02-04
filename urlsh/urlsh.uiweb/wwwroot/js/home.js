$(document).ready(function () {
    $("#txtUrl").blur(function () {
        $(this).val($(this).val().replace(/\s/g, ''));
        $("#urlForm").valid();
    });
    var defaultOptions = {
        errorClass: 'is-invalid',
        validClass: 'is-valid',
        errorLabelContainer: '.errDiv',
        highlight: function (element, errorClass, validClass) {
            $("#errorPlaceholder").hide();
            $(element).addClass(errorClass);
            $(element).removeClass(validClass);
        },
        unhighlight: function (element, errorClass, validClass) {
            $("#errorPlaceholder").show();
            $(element).addClass(validClass);
            $(element).removeClass(errorClass);
        }
    };
    var msgMinLength = function (params, element) {
        return 'Cannot be less than than ' + $(element).data('min') + ' characters.';
    };
    var msgValidUrl = function (params, element) {
        return 'Hmm this doesn\'t look like a url, it should start with http or https';
    };
    $.validator.addMethod('minLength', function (val, element) {
        return this.optional(element) || val.length >= $(element).data('min');
    }, msgMinLength);
    $.validator.addMethod('validUrl', function (val, element) {
        return re_weburl.test(val);
    }, msgValidUrl);
    $.validator.setDefaults(defaultOptions);
    // validate the comment form when it is submitted
    $("#urlForm").validate({
        rules: {
            txtUrlName: {
                required: true,
                minLength: true,
                validUrl: true
            }
        }
    });
    window.ko;
    console.log("ready!");
});
function cpToClip(text) {
    window.prompt("Copy to clipboard: Ctrl+C, Enter", text);
}
function TaskListViewModel() {
    var self = this;
    self.urls = ko.observableArray([]);
    self.visible = ko.observable(true); // Message initially visible
    self.newUrlText = ko.observable();
    self.PostUrl = function () {
        if ($("#urlForm").valid()) {
            $.post({
                url: "/Home/Zip",
                beforeSend: function (xhr) {
                    xhr.setRequestHeader("RequestVerificationToken",
                        $('input:hidden[name="__RequestVerificationToken"]').val());
                },
                data: { "": $("#txtUrl").val() },
                success: function (response) {
                    self.newUrlText(response);
                    self.urls.push(self.newUrlText());
                    self.newUrlText("");
                },
                failure: function (response) {
                    alert(response);
                },
                error: function (XMLHttpRequest, textStatus, errorThrown) {
                    alert("some error" + errorThrown + textStatus);
                }
            });
        }
    }
    self.addUrl = function () {
        self.PostUrl();
    };
}
ko.applyBindings(new TaskListViewModel());