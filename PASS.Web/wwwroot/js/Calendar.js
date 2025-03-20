document.addEventListener("DOMContentLoaded", function () {
    const calendarGrid = document.querySelector(".calendar-grid");
    const currentMonth = document.getElementById("currentMonth");
    const prevMonthBtn = document.getElementById("prevMonth");
    const nextMonthBtn = document.getElementById("nextMonth");
    const eventDetails = document.getElementById("eventDetails");
    const eventTitle = document.getElementById("eventTitle");
    const eventDescription = document.getElementById("eventDescription");
    const eventLink = document.getElementById("eventLink");

    let date = new Date();

    // Example events (Replace with dynamic data later)
    const events = {
        "2025-03-15": { title: "Sports Day", description: "Join us for a full day of sports events!", url: "/underconstruction" },
        "2025-03-20": { title: "Football Match", description: "Exciting match between top teams!", url: "/underconstruction" },
        "2025-03-25": { title: "Fun Run", description: "Get involved in our fun run!", url: "/underconstruction" }
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
                let eventBtn = document.createElement("button");
                eventBtn.textContent = events[eventDate].title;
                eventBtn.classList.add("event");

                // Click event to show event details
                eventBtn.addEventListener("click", function () {
                    eventTitle.textContent = events[eventDate].title;
                    eventDescription.textContent = events[eventDate].description;
                    eventLink.href = events[eventDate].url;
                    eventDetails.classList.add("show");
                });

                dayDiv.appendChild(eventBtn);
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
