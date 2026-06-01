
hs.showCredits = false;
hs.dimmingOpacity = 0.15;

hs.graphicsDir = 'https://docs.originlab.com/global/highslide/graphics/';
hs.align = 'center';
hs.transitions = ['expand', 'crossfade'];
hs.outlineType = 'rounded-white';
hs.fadeInOut = true;

hs.Expander.prototype.onAfterExpand = function () {
    var data = {
        page: location.href,
        link: this.a.href
    };
    ga('send', 'event', 'Slideshow - ' + this.slideshowGroup, 'Expand', JSON.stringify(data));
};

var availableGroupingOptions = {};
var createGalleryGroupingOption = function(g, moreOptions) {
    if ( availableGroupingOptions[g] )
    {
        return availableGroupingOptions[g];
    }
    else
    {
        var opt = {
            slideshowGroup: g,
            interval: 3000,
            repeat: false,
            useControls: true,
            fixedControls: 'fit',
            overlayOptions: {
                className: 'large-white',
                opacity: 0.75,
                position: 'top right',
                offsetX: 5,
                offsetY: -50,
                hideOnMouseOut: true
            },
            thumbstrip: {
                mode: 'horizontal',
                position: 'bottom left',
                relativeTo: 'viewport'
            }
        };
        
        if ( moreOptions )
        {
            for ( var k in moreOptions )
            {
                if ( moreOptions.hasOwnProperty(k) )
                {
                    opt[k] = moreOptions[k];
                }
            }
        }
        
        hs.addSlideshow(opt);
        return availableGroupingOptions[g] = opt;
    }
};

// gallery config object
var galleryOptions = createGalleryGroupingOption('Graph Gallery');

var createShowMeGroupingOption = function (g, moreOptions) {
    var opt = {
        thumbstrip: null,
        overlayOptions: {
            className: 'large-dark',
            opacity: 0.75,
            position: 'top right',
            offsetY: -50
        }
    };
    if (moreOptions) {
        for (var k in moreOptions) {
            if (moreOptions.hasOwnProperty(k)) {
                opt[k] = moreOptions[k];
            }
        }
    }
    return createGalleryGroupingOption(g, opt);
}