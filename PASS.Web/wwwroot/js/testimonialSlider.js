document.addEventListener("DOMContentLoaded", function () {
    const testimonials = [
        {
            text: "I feel more confident in knowing how to pace the lesson and link skills together for the whole lesson. Also to tailor lessons to suit all abilities using different equipment.",
            author: "Year 2 Teacher, St Katherine’s School & Nursery"
        },
        {
            text: "I have gained more confidence with every lesson of support. I enjoy the team teaching. I’ve learnt how to challenge the more able and assess through effective questioning.",
            author: "Year 2 Teacher, St Katherine’s School & Nursery"
        },
        {
            text: "These sessions have been great for building children’s skills when leading others. Tasks are broken down clearly and engage children in ownership of the task/game.",
            author: "Year 5 Teacher, Roseacre Junior School"
        },
        {
            text: "I’ve seen an increase in confidence, particularly from the less confident who now perform in front of the class. I’ve gained ideas to challenge more able pupils and structure progression.",
            author: "Year 6 Teacher, Roseacre Junior School"
        },
        {
            text: "Providing a comprehensive programme of PE promoting physical and mental well-being has really impacted the children.",
            author: "Year 3 Teacher, Meopham Community Academy"
        },
        {
            text: "Through the support I have a better understanding of the different skills required. Lessons are detailed, thorough, and led professionally.",
            author: "Year 3 Teacher, Meopham Community Academy"
        },
        {
            text: "I have increased my subject knowledge. I’m better able to plan and deliver PE lessons. Before, I would have placed a ceiling on what they could do.",
            author: "Year 1 Teacher, St Katherine’s School & Nursery"
        }
    ];

    const quoteText = document.getElementById("quote-text");
    const quoteAuthor = document.getElementById("quote-author");
    const quoteBtns = document.querySelectorAll(".quote-btn");

    // Initialize first quote and button
    let currentIndex = 0;

    function updateQuote(index) {
        // Set the new quote and author with fade
        quoteText.style.opacity = 0;
        quoteAuthor.style.opacity = 0;

        setTimeout(() => {
            quoteText.textContent = testimonials[index].text;
            quoteAuthor.textContent = testimonials[index].author;

            quoteText.style.opacity = 1;
            quoteAuthor.style.opacity = 1;

            quoteBtns.forEach((btn) => btn.classList.remove("active"));
            quoteBtns[index].classList.add("active");
        }, 250);
    }

    // Add event listeners to the buttons
    quoteBtns.forEach((btn, index) => {
        btn.addEventListener("click", () => {
            currentIndex = index;
            updateQuote(currentIndex);
        });
    });

    // --- Swipe Functionality ---
    let startX = 0;
    const display = document.querySelector(".testimonial-display");

    display.addEventListener("touchstart", (e) => {
        startX = e.touches[0].clientX;
    });

    display.addEventListener("touchend", (e) => {
        const endX = e.changedTouches[0].clientX;
        const diff = startX - endX;

        if (Math.abs(diff) > 50) { // swipe threshold
            if (diff > 0) {
                // Swipe left → next
                currentIndex = (currentIndex + 1) % testimonials.length;
            } else {
                // Swipe right → previous
                currentIndex = (currentIndex - 1 + testimonials.length) % testimonials.length;
            }
            updateQuote(currentIndex);
        }
    });

    // Initialize the first quote and active dot
    updateQuote(currentIndex);
});
