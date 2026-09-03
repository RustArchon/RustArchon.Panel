// Copyright ©2026 Scott Blomfield
//
// Thin Chart.js wrapper for ServerDetail.razor's Stats tab - matches consoleScroll.js's own pattern
// (a tiny global, not a collocated module) since nothing here needs per-component isolation.
//
// Chart.js instances aren't tracked by Blazor at all - the canvas elements they're attached to get
// torn down and recreated every time the Stats tab's @if block stops/starts rendering (see
// ServerDetail.razor's SwitchTabAsync remarks), so an instance from a previous visit would otherwise
// leak and, worse, throw "Canvas is already in use" if a stale one is still registered against a
// canvas id that's about to be reused. render() always destroys whatever it already tracked for that
// id first.

window.statsChart = {
    _instances: {},

    render: function (canvasId, config) {
        var canvas = document.getElementById(canvasId);
        if (!canvas || typeof Chart === "undefined") {
            return;
        }

        if (window.statsChart._instances[canvasId]) {
            window.statsChart._instances[canvasId].destroy();
        }

        config.options = Object.assign({
            responsive: true,
            maintainAspectRatio: false,
            animation: false,
            interaction: { mode: "index", intersect: false },
            scales: {
                x: { ticks: { color: "#9ca3af" }, grid: { color: "#ffffff1a" } },
                y: { ticks: { color: "#9ca3af" }, grid: { color: "#ffffff1a" }, beginAtZero: true }
            },
            plugins: {
                legend: { labels: { color: "#d1d5db" } }
            }
        }, config.options || {});

        window.statsChart._instances[canvasId] = new Chart(canvas.getContext("2d"), config);
    },

    destroyAll: function () {
        for (var id in window.statsChart._instances) {
            if (Object.prototype.hasOwnProperty.call(window.statsChart._instances, id)) {
                window.statsChart._instances[id].destroy();
            }
        }
        window.statsChart._instances = {};
    }
};
