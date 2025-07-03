document.addEventListener("DOMContentLoaded", () => {
    const yearMenu = document.getElementById("yearMenu");
    const categorySection = document.getElementById("categorySection");
    const categoryButtons = document.getElementById("categoryButtons");
    const videoSection = document.getElementById("videoSection");
    const videoGrid = document.getElementById("videoGrid");

    const backToYears = document.getElementById("backToYears");
    const backToCategories = document.getElementById("backToCategories");

    const categoryData = {
        "eyfs": ["gymnastics", "athletics", "tennis", "striking and fielding", "dance"],
        "y1-2": ["athletics", "striking and fielding", "tennis", "dance", "gymnastics", "multi skills", "control with feet"],
        "y3-4": ["athletics", "striking and fielding", "tennis", "dance", "fitness", "football", "gymnastics"],
        "y5-6": ["athletics", "striking and fielding", "tennis", "dance", "fitness", "games", "gymnastics"]
    };

    const playlistLinks = {
        "eyfs": {
            "gymnastics": "https://www.youtube.com/embed/videoseries?list=YOUR_PLAYLIST_ID"
        },
        "y1-2": {
            "athletics": "https://www.youtube.com/embed/videoseries?list=YOUR_PLAYLIST_ID"
        },
        "y3-4": {
            "dance": "https://www.youtube.com/embed/videoseries?list=YOUR_PLAYLIST_ID"
        },
        "y5-6": {
            "fitness": "https://www.youtube.com/embed/videoseries?list=YOUR_PLAYLIST_ID"
        }
    };

    let currentYearGroup = "";

    document.querySelectorAll(".year-btn").forEach(button => {
        button.addEventListener("click", () => {
            currentYearGroup = button.dataset.group;
            yearMenu.style.display = "none";
            videoSection.classList.add("hidden");
            categorySection.classList.remove("hidden");
            categoryButtons.innerHTML = "";

            categoryData[currentYearGroup].forEach(cat => {
                const btn = document.createElement("button");
                btn.textContent = cat.charAt(0).toUpperCase() + cat.slice(1);
                btn.dataset.category = cat;
                btn.addEventListener("click", () => {
                    loadVideos(currentYearGroup, cat);
                });
                categoryButtons.appendChild(btn);
            });
        });
    });

    backToYears.addEventListener("click", () => {
        categorySection.classList.add("hidden");
        yearMenu.style.display = "flex";
    });

    backToCategories.addEventListener("click", () => {
        videoSection.classList.add("hidden");
        categorySection.classList.remove("hidden");
        videoGrid.innerHTML = "";
    });

    function loadVideos(group, category) {
        const playlistUrl = playlistLinks[group]?.[category];
        if (!playlistUrl) {
            videoGrid.innerHTML = `<p>No videos found for this category.</p>`;
        } else {
            videoGrid.innerHTML = `
                <iframe src="${playlistUrl}" allowfullscreen></iframe>
            `;
        }

        categorySection.classList.add("hidden");
        videoSection.classList.remove("hidden");
    }
});
x