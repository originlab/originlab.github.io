(function (w) {

    function InsertExternalScript(src) {
        var script = document.createElement('script');
        script.type = 'text/javascript';
        script.src = src;
        var s = document.head.firstChild;
        s.parentNode.insertBefore(script, s);
    }

    function GetDebugFunction(title) {
        return function () {
            if (console) {
                console.log(title + ':');
                console.dir(Array.prototype.slice.call(arguments));
            }
        }
    }

    if (/originlab/ig.test(document.location.hostname)) {

        // Google Analytics
        w.GoogleAnalyticsObject = 'ga';
        w.ga = w.ga || function () {
            (w.ga.q = w.ga.q || []).push(arguments);
        };
        var ga = w.ga;
        ga('create', 'UA-1628093-1', 'auto');
        ga('send', 'pageview');
        InsertExternalScript('https://www.google-analytics.com/analytics.js');

        // Google tag
        w.dataLayer = w.dataLayer || [];
        function gtag() { dataLayer.push(arguments); }
        gtag('js', new Date());
        gtag('config', 'G-G0XNH6Y3D1');
        InsertExternalScript('https://www.googletagmanager.com/gtag/js?id=G-G0XNH6Y3D1');

    } else {
        w.ga = GetDebugFunction('Google Analytics');
        w.gtag = GetDebugFunction('Google Tag');
    }

})(window);

function showtip(current, e, text) {

    if (document.all || document.getElementById) {
        thetitle = text.split('<br>')
        if (thetitle.length > 1) {
            thetitles = ''
            for (i = 0; i < thetitle.length; i++)
                thetitles += thetitle[i]
            current.title = thetitles
        }
        else
            current.title = text
    }

    else if (document.layers) {
        document.tooltip.document.write('<layer bgColor="white" style="border:1px solid black;font-size:12px;">' + text + '</layer>')
        document.tooltip.document.close()
        document.tooltip.left = e.pageX + 5
        document.tooltip.top = e.pageY + 5
        document.tooltip.visibility = "show"
    }
}
function hidetip() {
    if (document.layers)
        document.tooltip.visibility = "hidden"
}

function clearText(oControl) {
    if (oControl.value == "Search" || oControl.value == "Suche" || oControl.value == "サイト内サーチ\t\t")
        oControl.value = "";
}

try {
    Function.emptyFunction = Function.emptyMethod = function Function$emptyMethod() {
        /// <summary locid="M:J#Function.emptyMethod" />
        //if (arguments.length !== 0) throw Error.parameterCount();
    }

    Sys.Net.XMLHttpExecutor.prototype.abort = function Sys$Net$XMLHttpExecutor$abort() {
        /// <summary locid="M:J#Sys.Net.XMLHttpExecutor.abort" />
        if (arguments.length !== 0) throw Error.parameterCount();
        if (!this._started) {
            throw Error.invalidOperation(Sys.Res.cannotAbortBeforeStart);
        }
        if (this._aborted || this._responseAvailable || this._timedOut)
            return;
        this._aborted = true;
        this._clearTimer();
        if (this._xmlHttpRequest && !this._responseAvailable) {

            var oldXhr = this._xmlHttpRequest;
            if (typeof (oldXhr.msCaching) != "undefined") {
                oldXhr.onreadystatechange = function () {
                    if (oldXhr.readyState > 0) {
                        setTimeout(function () {
                            oldXhr.abort();
                            oldXhr = null;
                        }, 0);
                        oldXhr.onreadystatechange = Function.emptyMethod;
                    }
                };
            } else {
                oldXhr.abort();
            }

            this._xmlHttpRequest = null;
            this._webRequest.completed(Sys.EventArgs.Empty);
        }
    }
}
catch (e) { }

(function () {

    var groupId = -1;

    var cdn = /\.originlab\.com/i.test(document.location.hostname) ? "//d2mvzyuse3lwjc.cloudfront.net" : "";

    function requestSequence(element, folder, icon) {
        groupId++;
        var callbackName = "imgSeq_" + groupId;
        var callbackSite = document.createElement("script");
        callbackSite.src = "/imgseq.ashx?f=" + folder + "&i=" + groupId;
        window[callbackName] = imgSeq(callbackName, callbackSite, element, folder, icon);
        document.body.appendChild(callbackSite);
    }

    function imgSeq(callbackName, callbackSite, element, folder, _icon) {
        return function (items) {
            var container = $("<div>");
            if (_icon && _icon != "") {
                _icon = "_" + _icon;
            } else {
                _icon = "";
            }
            $.each(items, function (idx, item) {
                var path = cdn + "/www/products/images/imgSeqs/" + folder + "/" + item.img;
                var icon = cdn + "/images/showme_icon" + _icon + ".png"
                var img = "<a class='highslide' href='" + path + "' onclick='return hs.expand(this, createShowMeGroupingOption(\"" + callbackName + "\"))'><img src='" + icon + "' style='border-style: none'></a>";
                var cap = "<div class='highslide-caption'>";

                var actualContainer = (idx == 0) ? $(element) : container;
                actualContainer.append(img);
                if (item.cap) actualContainer.append($(cap).html(item.cap));
            });
            container.hide().appendTo(element);

            document.body.removeChild(callbackSite);
            delete window[callbackName];
        };
    }

    $(function () {
        $(".showMeButton").each(function () {
            requestSequence(this, $(this).attr("imgSeq-container"), $(this).attr("imgSeq-icon"));
        });
    });

})();

(function () {
    if (typeof (hsSimpleCloseButton) != 'undefined' && typeof (hs) != 'undefined') {
        $(function () {
            $(".highslide-caption").each(function () {
                var current = $(this);
                var clone = current.clone().appendTo(current.parent());
                clone.removeClass("highslide-caption").addClass("hsFrame-caption");
            });
        });
        hs.Expander.prototype.onBeforeExpand = function (expander, e) {
            if (expander.custom == hsSimpleCloseButton) {
                var closeButtonContainer = document.createElement("div");
                closeButtonContainer.innerHTML = '<div class="closebutton" onclick="return hs.close(this)" title="Close"></div>';
                expander.createOverlay({
                    overlayId: closeButtonContainer,
                    position: "top right"
                });
            }
        };
    }
})()

function getQueryString(name, url) {
    if (!url) url = window.location.href;
    name = name.replace(/[\[\]]/g, "\\$&");
    var regex = new RegExp("[?&]" + name + "(=([^&#]*)|&|#|$)"),
        results = regex.exec(url);
    if (!results) return null;
    if (!results[2]) return '';
    return decodeURIComponent(results[2].replace(/\+/g, " "));
}

// video click to play
/*
$(function () {
    $('.video').click(function () {
        var wrapper = $(this);
        var video = wrapper.find('video')[0];
        if (video.paused) {
            wrapper.removeClass('paused');
            video.play();
        } else {
            wrapper.addClass('paused');
            video.pause();
        }
    }).find('video').on('ended', function () {
        $(this).parent().addClass('paused');
    });
});
*/

// Common email tld typo suggest
(function (window) {
    var commonTLDs = ['hotmail', 'gmail', 'outlook', 'originlab'];

    function TryFixEmailTypo(input) {
        if (typeof(input) == 'string') {
            input = document.getElementById(input);
        }
        var email = input.value;
        if (email.indexOf('@') > 0) {
            var tld = /@([^\.]+)/.exec(input.value)[1];
            var suggestedTLD = TrySuggestTldTypoFix(tld);
            if (suggestedTLD != null) {
                var fixItMessage = 'We detected possible typo in your email: ' + email + '\n\n';
                fixItMessage += 'Instead of: "' + tld + '"\n';
                fixItMessage += 'Did you mean: "' + suggestedTLD + '"?\n\n';
                fixItMessage += 'OK = Fix it for me\n';
                fixItMessage += "Cancel = That's fine";
                if (confirm(fixItMessage)) {
                    var fixed = email.replace(/@[^\.]+/, '@' + suggestedTLD);
                    input.value = fixed;
                }
            }
        }
        return true;
    }
    function TrySuggestTldTypoFix(tld) {
        for (var t = 0; t < commonTLDs.length; t++) {
            var targetTLD = commonTLDs[t];

            // We don't detect changes involving the first or the last letter, as such typos are generally easy to spot
            if (tld[0] != targetTLD[0] || tld[tld.length - 1] != targetTLD[targetTLD.length - 1]) {
                continue;
            }

            var mistakes = [];
            Array.prototype.push.apply(mistakes, Swapped(targetTLD));
            Array.prototype.push.apply(mistakes, Repeated(targetTLD));
            Array.prototype.push.apply(mistakes, Deleted(targetTLD));

            var protential = mistakes.indexOf(tld);
            if (protential > -1) {
                return targetTLD;
            }
        }
        return null;
    }
    function Swapped(targetTLD) {
        var result = [];
        if (targetTLD.length < 4) {
            return result;
        }
        for (var i = 3; i < targetTLD.length; i++) {
            var typo = targetTLD.substr(0, i - 2);
            typo += targetTLD[i - 1] + targetTLD[i - 2];
            typo += targetTLD.substr(i);
            if (typo != targetTLD) {
                result.push(typo);
            }
        }
        return result;
    }
    function Repeated(targetTLD) {
        var result = [];
        for (var i = 0; i < targetTLD.length; i++) {
            var typo = targetTLD.substr(0, i);
            typo += targetTLD[i];
            typo += targetTLD.substr(i);
            result.push(typo);
        }
        return result;
    }
    function Deleted(targetTLD) {
        var result = [];
        for (var i = 1; i < targetTLD.length - 1; i++) {
            var typo = targetTLD.substr(0, i);
            typo += targetTLD.substr(i + 1);
            result.push(typo);
        }
        return result;
    }

    window.TryFixEmailTypo = TryFixEmailTypo;
})(window);


jQuery(document).ready(function () {
    var offset = 220;
    var duration = 300;
    jQuery(window).scroll(function () {
        if (jQuery(this).scrollTop() > offset) {
            jQuery('.back-to-top').fadeIn(duration);
        } else {
            jQuery('.back-to-top').fadeOut(duration);
        }
    });

    jQuery('.back-to-top').click(function () {
        jQuery('body,html').animate({
            scrollTop: 0
        }, 800);
        return false;
    });

    if (typeof domReady != "undefined") {
        for (var i = 0; i < domReady.length; i++) {
            domReady[i]();
        }
    }
});
