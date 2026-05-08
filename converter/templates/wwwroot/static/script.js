function searchSite(inputId) {
    var terms = document.getElementById(inputId).value;
    document.location.href = "https://www.google.com/search?sitesearch=originlab.com&q=" + encodeURIComponent(terms);
}

function searchDoc(lang, inputId, selectId) {
    var terms = document.getElementById(inputId).value;
    var book = document.getElementById(selectId).value;
    var searchUrl = '';
    if (book != '') {
        switch (lang) {
            case 'zh':
                searchUrl = `https://www.bing.com/search?q=${encodeURIComponent(terms)}+site:docs.originlab.com/${book}`;
                break;
            case 'ja':
                searchUrl = `https://www.google.co.jp/search?sitesearch=docs.originlab.com/${book}&lr=lang_ja&q=` + encodeURIComponent(terms);
                break;
            case 'de':
                searchUrl = `https://www.google.de/search?sitesearch=docs.originlab.com/${book}&q=` + encodeURIComponent(terms);
                break;
            default:
                searchUrl = `https://www.google.com/search?sitesearch=docs.originlab.com/${book}&q=` + encodeURIComponent(terms);
        }
    } else {
        switch (lang) {
            case 'zh':
                searchUrl = `https://www.bing.com/search?q=${encodeURIComponent(terms)}+site:docs.originlab.com`;
                break;
            case 'ja':
                searchUrl = `https://www.google.co.jp/search?sitesearch=docs.originlab.com&lr=lang_ja&q=` + encodeURIComponent(terms);
                break;
            case 'de':
                searchUrl = `https://www.google.de/search?sitesearch=docs.originlab.com&q=` + encodeURIComponent(terms);
                break;
            default:
                searchUrl = `https://www.google.com/search?sitesearch=docs.originlab.com&q=` + encodeURIComponent(terms);
        }
    }
    document.location.replace(searchUrl);
}

function applyNavMenuData(dataId, groupId, minItems) {
    let dataElement = document.getElementById(dataId);
    if (!dataElement || dataElement.children.length < minItems) {
        document.getElementById(groupId)?.querySelector('button')?.classList.add('disabled');
    } else {
        document.getElementById(groupId)?.querySelector('ul')?.replaceChildren(...dataElement.childNodes);
    }
}

(function () {
    let familyData = document.getElementById('doc-nav-data');
    let parentLink = familyData.getAttribute('data-parent-link');
    let parentBtn = document.getElementById('doc-nav-parent');
    if (parentBtn) {
        parentBtn.setAttribute('href', parentLink);
    }

    applyNavMenuData('doc-siblings-data', 'doc-nav-siblings', 2);
    applyNavMenuData('doc-children-data', 'doc-nav-children', 1);
})();
