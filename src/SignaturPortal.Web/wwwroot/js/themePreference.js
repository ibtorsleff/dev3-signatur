window.themePreference = {
    get: () => localStorage.getItem('theme-preference'),
    set: (value) => localStorage.setItem('theme-preference', value),
};
