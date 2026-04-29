function searchSite(inputId) {
    var terms = document.getElementById(inputId).value;
    document.location.href = "https://www.google.com/search?sitesearch=originlab.com&q=" + encodeURIComponent(terms);
}

function searchDoc(lang, inputId, selectId) {
    var terms = document.getElementById(inputId).value;
    var book = document.getElementById(selectId).value;
    if (lang != 'en') {
        document.location.href = `https://www.google.com/search?sitesearch=docs.originlab.com/${book}/${lang}&q=` + encodeURIComponent(terms);
    } else {
        document.location.href = `https://www.google.com/search?sitesearch=docs.originlab.com/${book}&q=` + encodeURIComponent(terms);
    }
}