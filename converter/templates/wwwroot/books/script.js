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

(function () {
    let nav = document.getElementById('doc-nav-data');

    let parentLink = nav.getAttribute('data-parent-link');
    let parentBtn = document.getElementById('doc-nav-parent');
    if (parentBtn) {
        parentBtn.setAttribute('href', parentLink);
    }

    let bookIndex = nav.getAttribute('data-book-index');
    if (bookIndex) {
        let data = document.getElementById('doc-siblings-data');
        if (data.childElementCount > 0) {
            data.firstElementChild.remove();
        }
        let allBooks = [...document.getElementById('docSearchBook').querySelectorAll('option')];
        let lang = nav.getAttribute('data-lang');
        for (let i = 1; i < allBooks.length; i++) {
            let book = allBooks[i];
            let li = document.createElement('li');
            let a = document.createElement('a');

            a.text = book.text;
            if (book.value.toLocaleLowerCase() == bookIndex) {
                li.className = 'disabled';
            } else {
                a.href = lang == 'en' ? `/${book.value.toLowerCase()}/` : `/${book.value.toLowerCase()}/${lang}/`;
            }

            li.appendChild(a);
            data.appendChild(li);
        }
        nav.appendChild(data);
    }

    function applyNavMenuData(dataId, groupId, minItems) {
        let dataElement = document.getElementById(dataId);
        if (!dataElement || dataElement.children.length < minItems) {
            document.getElementById(groupId)?.querySelector('button')?.classList.add('disabled');
        } else {
            let ul = document.getElementById(groupId)?.querySelector('ul');
            if (ul) {
                ul.replaceChildren(...dataElement.childNodes);
                if (ul.childNodes.length > 20) {
                    ul.classList.add('pre-scrollable');
                }
            }
        }
    }

    applyNavMenuData('doc-siblings-data', 'doc-nav-siblings', 2);
    applyNavMenuData('doc-children-data', 'doc-nav-children', 1);
})();

window.MathJax = {
    options: {
        processHtmlClass: 'tex',
        ignoreHtmlClass: '.*'
    }
};
