window.appTheme = {
    apply(theme) {
        const normalizedTheme = theme === "dark" ? "dark" : "light";

        document.documentElement.setAttribute("data-bs-theme", normalizedTheme);
        document.body.setAttribute("data-bs-theme", normalizedTheme);
        document.documentElement.style.colorScheme = normalizedTheme;
    }
};
