document.addEventListener("DOMContentLoaded", () => {
    const buttons = document.querySelectorAll(".year-btn");
    const menu = document.getElementById("menu");
    const videoSection = document.getElementById("videoSection");
    const videoGrid = document.getElementById("videoGrid");
    const backButton = document.getElementById("backButton");

    const videoData = {
        "eyfs": [
            "https://www.youtube.com/embed/XXXXXX1",
            "https://www.youtube.com/embed/XXXXXX2"
        ],
        "y1-2": [
            "https://www.youtube.com/embed/XXXXXX3",
            "https://www.youtube.com/embed/XXXXXX4"
        ],
        "y3-4": [
            "https://www.youtube.com/embed/XXXXXX5",
            "https://www.youtube.com/embed/XXXXXX6"
        ],
        "y5-6": [
            "https://www.youtube.com/embed/XXXXXX7",
            "https://www.youtube.com/embed/XXXXXX8"
        ]
    };

    buttons.forEach(btn => {
        btn.addEventListener("click", () => {
            const group = btn.dataset.group;

            menu.style.display = "none";
            videoSection.classList.remove("hidden");

            videoGrid.innerHTML = ""; // Clear old videos

            videoData[group].forEach(url => {
                const iframe = document.createElement("iframe");
                iframe.src = url;
                iframe.allow = "accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture";
                iframe.allowFullscreen = true;
                videoGrid.appendChild(iframe);
            });
        });
    });

    backButton.addEventListener("click", () => {
        videoSection.classList.add("hidden");
        menu.style.display = "flex";
        videoGrid.innerHTML = "";
    });
});
