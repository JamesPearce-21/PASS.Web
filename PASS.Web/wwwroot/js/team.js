document.addEventListener("DOMContentLoaded", () => {
    const cards = document.querySelectorAll(".team-card");

    cards.forEach(card => {
        const inner = card.querySelector(".card-inner");

        card.addEventListener("click", () => {
            // Only toggle the clicked card
            inner.classList.toggle("flipped");
        });
    });
});
