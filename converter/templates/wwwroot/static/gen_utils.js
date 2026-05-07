function DOMContentLoadedEvent() {
    return new Promise(resolve => window.addEventListener("DOMContentLoaded", event => resolve(event), { once: true }));
}

async function fetchApplyLayout(layoutUrl, placeholderId, mainContentId, parent, children) {
    var domLoadEvent = DOMContentLoadedEvent();
    var response = await fetch(layoutUrl);
    var parser = new DOMParser();
    var layout = parser.parseFromString(await response.text(), 'text/html');
    var placeholder = layout.getElementById(placeholderId);
    var scripts = [...layout.scripts];

    for (var s of scripts) {
        s.remove();
    }

    await domLoadEvent;

    var title = document.title;

    placeholder.replaceWith(...document.getElementById(mainContentId).childNodes);
    document.replaceChild(
        document.adoptNode(layout.documentElement),
        document.documentElement
    );

    document.title = title;

    for (var s of scripts) {
        var script = document.createElement("script");
        script.async = s.async;
        if (s.src != "") {
            script.src = s.src;
        } else {
            script.innerHTML = s.innerHTML;
        }
        document.body.append(script);
    };
}

function applyParentChildren(parent, children) {
    if (parent) {
        let parentBtn = document.getElementById('doc-btn-parent');
        if (parentBtn) {
            parentBtn.setAttribute('href', parent);
            parentBtn.classList.remove('hidden');
        }
    }

    if (children && children.length > 0) {
        let childrenGroup = document.getElementById('doc-btn-children');
        if (childrenGroup) {
            let ul = childrenGroup.querySelector('ul');
            for (var item of children) {
                let li = document.createElement('li');
                let a = document.createElement('a');

                a.href = item.href;
                a.text = item.text;

                li.appendChild(a);
                ul.appendChild(li);
            }
        }
    }
}

function tryRedirectToLowerOrEnglish() {
    var currentURL = window.location.href;
    var lowerCaseURL = currentURL.toLowerCase();
    if (currentURL != lowerCaseURL) {
        location.replace(lowerCaseURL);
    } else if (currentURL.lastIndexOf('/') == currentURL.length - 3) {
        location.replace(currentURL.substring(0, currentURL.lastIndexOf('/')));
    }
}