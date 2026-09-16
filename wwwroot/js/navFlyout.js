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
// only JS can supply, since every trigger - including a nested one like an area's Reports entry,
// which opens a further flyout of its own - sits at its own height in a scrollable list, or floats
// wherever its own parent flyout currently does.
//
// Delegated listeners on document, not bound to the nav elements directly, so this keeps working
// across Blazor's enhanced-navigation DOM patches without needing to re-attach anything.
(function () {
    function positionFlyout(group) {
        // :scope > rather than a plain descendant query - a Reports entry nests a further
        // .nav-flyout-group of its own now (see NavMenu.razor), so an unscoped query here could
        // find that nested group's trigger/menu instead of this group's own, depending on document
        // order. Either kind of trigger a group can have - the plain <button> of an area like
        // Customers, or the real <NavLink> of a nested Reports entry - is still just one element
        // whose real on-screen position this reads, so the same function positions every level.
        var toggle = group.querySelector(":scope > .nav-subgroup-toggle, :scope > .nav-link");
        var menu = group.querySelector(":scope > .nav-flyout-menu");
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
            // Re-entering a group the last click force-closed (below) means the pointer has come
            // back around to it on purpose - let it open normally again.
            group.classList.remove("nav-flyout-force-closed");
            positionFlyout(group);
        }
    }

    function onFlyoutLinkClick(event) {
        var link = event.target.closest && event.target.closest(".nav-flyout-menu a");
        var group = link && link.closest(".nav-flyout-group");
        if (!group) {
            return;
        }

        // Clicking a link here navigates via Blazor's enhanced navigation, which patches the
        // sidebar's DOM back in afterwards - and the patched markup never had the top/left this
        // script set, since the server never rendered them. Left alone, the menu snaps back to its
        // unset default position (pinned top-left) a moment later. It would also stay visible
        // there: the clicked link keeps real browser focus across the patch (same DOM node, only
        // its "active" class changes), so :focus-within alone doesn't close it, and :hover does not
        // re-evaluate until the pointer physically moves - which is exactly what let the stranded
        // menu reappear (repositioned by the handler above, since the pointer crosses back over its
        // trigger) as the pointer traveled toward whatever menu got hovered next. Forcing it closed
        // right here, at the moment of the click, sidesteps that race entirely instead of trying to
        // win it.
        //
        // Walks every ancestor flyout group the click was nested inside, not just the nearest one -
        // a report link now sits two levels deep (its area, then Reports), and both levels are open
        // to have gotten there, so both need force-closing or the outer one would strand the exact
        // same way the inner one used to before this fix existed.
        while (group) {
            group.classList.add("nav-flyout-force-closed");
            group = group.parentElement && group.parentElement.closest(".nav-flyout-group");
        }

        if (document.activeElement && document.activeElement.blur) {
            document.activeElement.blur();
        }
    }

    document.addEventListener("mouseover", onPointerOrFocus);
    document.addEventListener("focusin", onPointerOrFocus);
    document.addEventListener("click", onFlyoutLinkClick);

    // A trigger's own vertical position can move (a translation makes a label longer/shorter, the
    // window resizes) while a flyout is already open - keep it glued to its trigger rather than
    // stranding it at a stale coordinate.
    window.addEventListener("resize", function () {
        document.querySelectorAll(".nav-flyout-group").forEach(positionFlyout);
    });
})();
