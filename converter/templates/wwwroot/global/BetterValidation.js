
function ValidateGroupForNonGroupedSubmit(precheckGroup) {
    return function (validator, args) {
        var textbox = document.getElementById(validator.controltovalidate);
        var precheckers = textbox.Validators;
        var isPrecheckValid = true;
        for (var i = 0; i < precheckers.length; i++) {
            ValidatorValidate(precheckers[i], precheckGroup, null);
            isPrecheckValid = (isPrecheckValid && precheckers[i].isvalid);
        }
        args.IsValid = isPrecheckValid;
    };
}

(function () {

    var orgValidatorOnChange = ValidatorOnChange;
    ValidatorOnChange = function (e) {
        orgValidatorOnChange(e);

        ValidationSummaryOnSubmit(null);
        UpdateControlsValidState();
    };

    var orgValidationSummaryOnSubmit = ValidationSummaryOnSubmit;
    ValidationSummaryOnSubmit = function (validationGroup) {
        if (typeof (Page_ValidationSummaries) == "undefined") {
            return;
        }

        if (typeof (validationGroup) == "undefined" || validationGroup == null) {
            orgValidationSummaryOnSubmit(validationGroup);
            return;
        }

        var visableVSsInOtherGroups = $(Page_ValidationSummaries).filter(function (i, summary) {
            return !IsValidationGroupMatch(summary, validationGroup)
        }).filter(":visible");

        orgValidationSummaryOnSubmit(validationGroup);

        visableVSsInOtherGroups.show();
    };

    Page_ClientValidate = function (validationGroup) {
        Page_InvalidControlToBeFocused = null;
        if (typeof (Page_Validators) == "undefined") {
            return true;
        }

        var isGroupValid = true;
        for (var i = 0; i < Page_Validators.length; i++) {
            var validator = Page_Validators[i];
            if (!IsValidationGroupMatch(validator, validationGroup)) {
                continue;
            }
            ValidatorValidate(Page_Validators[i], validationGroup, null);
            isGroupValid = (isGroupValid && validator.isvalid);
        }

        Page_IsValid = (Page_IsValid && isGroupValid);
        Page_BlockSubmit = !isGroupValid;

        ValidationSummaryOnSubmit(validationGroup);
        UpdateControlsValidState();

        return isGroupValid;
    };

    function UpdateControlsValidState() {
        var isValid = {};
        for (var i = 0; i < Page_Validators.length; i++) {
            var validator = Page_Validators[i];
            var ctrl = validator.controltovalidate;
            if (typeof (isValid[ctrl]) == "undefined") {
                isValid[ctrl] = validator.isvalid;
            } else {
                isValid[ctrl] = (isValid[ctrl] && validator.isvalid);
            }
        }
        for (id in isValid) {
            if (!isValid.hasOwnProperty(id)) {
                continue;
            }
            var targets = $("#" + id);
            targets = targets.add(targets.closest(".form-group").find(".control-label")[0]);
            targets.toggleClass("validation-fail", !isValid[id]);
        }
    }

    var orgValidatorOnLoad = ValidatorOnLoad;
    ValidatorOnLoad = function () {
        if (typeof (Page_Validators) == "undefined") {
            return;
        }
        if (typeof ($) == "undefined") {
            setTimeout(ValidatorOnLoad, 200);
            return;
        }

        for (var i = 0; i < Page_Validators.length; i++) {
            var validator = Page_Validators[i];
            if (validator.isvalid === false) {
                validator.isvalid = "False";
            }
        }

        orgValidatorOnLoad();

        if (!Page_IsValid) {
            ValidationSummaryOnSubmit(null);
            UpdateControlsValidState();
        }
    };

})();
