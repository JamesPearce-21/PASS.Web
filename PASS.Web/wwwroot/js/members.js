document.addEventListener("DOMContentLoaded", function () {
    const loginForm = document.querySelector(".login-form");
    const loginWrapper = document.querySelector(".login-contact-wrapper");
    const membersContent = document.querySelector(".members-content");
    const errorBox = document.querySelector(".login-error");

    if (!loginForm || !membersContent || !loginWrapper) return;

    membersContent.style.display = "none"; // Hide members content initially

    loginForm.addEventListener("submit", function (e) {
        e.preventDefault();
        const username = loginForm.querySelector("input[name='username']").value.trim();
        const password = loginForm.querySelector("input[name='password']").value.trim();

        if (username === "TEST" && password === "TEST") {
            loginWrapper.style.display = "none";
            membersContent.style.display = "block";
            errorBox.style.display = "none";
            window.scrollTo({ top: 0, behavior: "smooth" });
        } else {
            errorBox.style.display = "block";
        }
    });
});
