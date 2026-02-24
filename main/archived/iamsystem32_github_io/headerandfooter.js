
document.addEventListener('DOMContentLoaded', function () {
    loadinit();
});

function loadinit() {
    fetch('./headerandfooter.html')
        .then(response => { return response.text(); })
        .then(html => { document.querySelector('.fullscreen-container').insertAdjacentHTML('beforeend', html); })
}