document.addEventListener("DOMContentLoaded", function () {
    const carousel = document.querySelector(".brand-carousel");
    if (!carousel) return; // Ensure carousel exists

    const items = Array.from(carousel.children);

    // Clone items dynamically for an infinite effect
    items.forEach((item) => {
        let clone = item.cloneNode(true);
        clone.setAttribute("aria-hidden", "true"); // Accessibility
        carousel.appendChild(clone);
    });

    // Ensure smooth reset when items exit the left side
    function resetCarousel() {
        const firstItem = carousel.children[0];
        if (firstItem.getBoundingClientRect().right < 0) {
            carousel.appendChild(firstItem); // Move the first item to the end
        }
    }

    setInterval(resetCarousel, 50); // Check every 50ms for smooth looping
});
