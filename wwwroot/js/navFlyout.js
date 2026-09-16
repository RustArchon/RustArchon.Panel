// Copyright ©2026 Scott Blomfield
//
// Positions a hovered/focused sidebar nav flyout menu (see NavMenu.razor's "Site administration"
// subgroups) as position:fixed next to its trigger, at the trigger's own on-screen coordinates.
//
// Why this needs JS at all rather than being pure CSS like the rest of this app's interactivity:
// the flyout has to escape .nav-scrollable's own overflow-y: auto (a tall menu needs to scroll), and
// per the CSS overflow spec, a non-visible value on one axis forces the other axis to also clip rather
// than staying visible - so a plain position:absolute flyout trying to escape sideways gets silently
// clipped at the sidebar's own edge instead of floating over the page. position:fixed escapes every
// ancestor's overflow clipping by design, but unlike position:absolute (which inherits an offset
// relative to its positioned ancestor for free), position:fixed needs real viewport coordinates - which
// only JS can supply, since three different trigger buttons sit at three different heights in a
// scrollable list.
//
// Delegated listeners on document, not bound to the nav elements directly, so this keeps working
// across Blazor's enhanced-navigation DOM patches without needing to re-attach anything.
(function () {
    function positionFlyout(group) {
        var toggle = group.querySelector(".nav-subgroup-toggle");
        var menu = group.querySelector(".nav-flyout-menu");
        if (!toggle || !menu) {
            return;
        }

        var rect = toggle.getBoundingClientRect();
        menu.style.top = rect.top + "px";
        menu.style.left = rect.right + "px";
    }

    function onPointerOrFocus(event) {
        var group = event.target.closest && event.target.closest(".nav-flyout-group");
        if (group) {
            positionFlyout(group);
        }
    }

    document.addEventListener("mouseover", onPointerOrFocus);
    document.addEventListener("focusin", onPointerOrFocus);

    // A trigger's own vertical position can move (a translation makes a label longer/shorter, the
    // window resizes) while a flyout is already open - keep it glued to its trigger rather than
    // stranding it at a stale coordinate.
    window.addEventListener("resize", function () {
        document.querySelectorAll(".nav-flyout-group").forEach(positionFlyout);
    });
})();
