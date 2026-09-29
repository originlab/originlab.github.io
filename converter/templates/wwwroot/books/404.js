
function handle404() {
    var currentURL = window.location.href;
    var lowerCaseURL = currentURL.toLowerCase();
    if (currentURL != lowerCaseURL) {
        location.replace(lowerCaseURL);
    } else if (/\/\w{2}\/?$/.test(currentURL.replace(/[#?&].*/, ''))) {
        location.replace(currentURL.substring(0, currentURL.lastIndexOf('/', currentURL.length - 2)));
    } else {
        const languagePreference = navigator.languages || [navigator.language || navigator.userLanguage || navigator.browserLanguage];
        let actual = 'en';
        for (let prefer of languagePreference) {
            if (prefer.startsWith('ja')) {
                actual = 'ja';
                break;
            } else if (prefer.startsWith('de')) {
                actual = 'de';
                break;
            } else if (prefer.startsWith('zh')) {
                actual = 'zh';
                break;
            }
        }
        location.replace(`/${actual}/404.html#${encodeURIComponent(location.pathname)}`);
    }
}
