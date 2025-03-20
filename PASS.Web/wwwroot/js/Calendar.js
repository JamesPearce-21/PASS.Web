document.addEventListener("DOMContentLoaded", function () {
    const calendarGrid = document.querySelector(".calendar-grid");
    const currentMonth = document.getElementById("currentMonth");
    const prevMonthBtn = document.getElementById("prevMonth");
    const nextMonthBtn = document.getElementById("nextMonth");

    let date = new Date();

    // Example events (Replace with dynamic data later)
    const events = {
        "2025-03-15": { title: "Sports Day", url: "/events/sports-day" },
        "2025-03-20": { title: "Football Match", url: "/events/football-match" },
        "2025-03-25": { title: "Community Fun Run", url: "/events/community-run" }
    };

    function renderCalendar() {
        const year = date.getFullYear();
        const month = date.getMonth();
        const firstDay = new Date(year, month, 1).getDay();
        const daysInMonth = new Date(year, month + 1, 0).getDate();

        currentMonth.textContent = date.toLocaleString("default", { month: "long", year: "numeric" });

        calendarGrid.innerHTML = ""; // Clear previous grid

        // Fill in empty days before the first of the month
        for (let i = 0; i < firstDay; i++) {
            let emptyDiv = document.createElement("div");
            emptyDiv.classList.add("empty");
            calendarGrid.appendChild(emptyDiv);
        }

        // Generate days
        for (let day = 1; day <= daysInMonth; day++) {
            let dayDiv = document.createElement("div");
            dayDiv.classList.add("calendar-day");
            dayDiv.textContent = day;

            let eventDate = `${year}-${String(month + 1).padStart(2, "0")}-${String(day).padStart(2, "0")}`;

            if (events[eventDate]) {
                let eventLink = document.createElement("a");
                eventLink.href = events[eventDate].url;
                eventLink.textContent = events[eventDate].title;
                eventLink.classList.add("event");
                dayDiv.appendChild(eventLink);
            }

            calendarGrid.appendChild(dayDiv);
        }
    }

    prevMonthBtn.addEventListener("click", function () {
        date.setMonth(date.getMonth() - 1);
        renderCalendar();
    });

    nextMonthBtn.addEventListener("click", function () {
        date.setMonth(date.getMonth() + 1);
        renderCalendar();
    });

    renderCalendar();
});
