// Scroll to element by ID with smooth behavior
window.scrollToElement = function (elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.scrollIntoView({
            behavior: 'smooth',
            block: 'center',
            inline: 'nearest'
        });
    }
};

// Toggle sidebar visibility on mobile
window.toggleSidebar = function () {
    const sidebar = document.getElementById('sidebar');
    const backdrop = document.querySelector('.sidebar-backdrop');

    if (sidebar && backdrop) {
        sidebar.classList.toggle('open');
        backdrop.classList.toggle('active');

        // Prevent body scroll when sidebar is open
        if (sidebar.classList.contains('open')) {
            document.body.style.overflow = 'hidden';
        } else {
            document.body.style.overflow = '';
        }
    }
};

// Close sidebar (used by backdrop click)
window.closeSidebar = function () {
    const sidebar = document.getElementById('sidebar');
    const backdrop = document.querySelector('.sidebar-backdrop');

    if (sidebar && backdrop) {
        sidebar.classList.remove('open');
        backdrop.classList.remove('active');
        document.body.style.overflow = '';
    }
};
