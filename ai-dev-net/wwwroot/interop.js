// Minimal JS interop for localStorage
window.decisionInterop = {
    setLastRead: function (projectSlug, timestamp) {
        localStorage.setItem('decisions-last-read-' + projectSlug, timestamp);
    },
    getLastRead: function (projectSlug) {
        return localStorage.getItem('decisions-last-read-' + projectSlug);
    }
};
