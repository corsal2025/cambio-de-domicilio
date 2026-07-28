// Renders every /Estadisticas chart from the server-computed JSON blob in #stats-data.
// All aggregation happens server-side (EstadisticasModel.OnGet + StatisticsService) — this file
// only shapes DTOs into Chart.js configs, it never computes business data itself.
(function () {
    var data = JSON.parse(document.getElementById('stats-data').textContent);

    var palette = {
        pending: '#6b7280',
        uploaded: '#926100',
        confirmed: '#1e7a3d',
        primary: '#0b3d68',
        accent: '#1a73b0',
        danger: '#a11d33',
        ok: '#1e7a3d',
        muted: '#d7dde3'
    };

    function donut(canvasId, labels, values, colors) {
        var el = document.getElementById(canvasId);
        if (!el) return;
        new Chart(el, {
            type: 'doughnut',
            data: { labels: labels, datasets: [{ data: values, backgroundColor: colors }] },
            options: { plugins: { legend: { position: 'bottom' } } }
        });
    }

    function bar(canvasId, labels, values, colors) {
        var el = document.getElementById(canvasId);
        if (!el) return;
        new Chart(el, {
            type: 'bar',
            data: { labels: labels, datasets: [{ data: values, backgroundColor: colors || palette.primary }] },
            options: { plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true, ticks: { precision: 0 } } } }
        });
    }

    function horizontalBar(canvasId, labels, values, color) {
        var el = document.getElementById(canvasId);
        if (!el) return;
        new Chart(el, {
            type: 'bar',
            data: { labels: labels, datasets: [{ data: values, backgroundColor: color || palette.accent }] },
            options: { indexAxis: 'y', plugins: { legend: { display: false } }, scales: { x: { beginAtZero: true, ticks: { precision: 0 } } } }
        });
    }

    donut('chart-status', ['Pendiente', 'Subido', 'Confirmado'],
        [data.status.pending, data.status.uploaded, data.status.confirmed],
        [palette.pending, palette.uploaded, palette.confirmed]);

    var weekly = data.weekly || [];
    var el = document.getElementById('chart-weekly');
    if (el) {
        new Chart(el, {
            type: 'line',
            data: {
                labels: weekly.map(function (w) { return w.weekStart; }),
                datasets: [{ data: weekly.map(function (w) { return w.count; }), borderColor: palette.primary, backgroundColor: palette.primary, tension: 0.2 }]
            },
            options: { plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true, ticks: { precision: 0 } } } }
        });
    }

    var comunas = data.comunas || [];
    horizontalBar('chart-comunas', comunas.map(function (c) { return c.comuna; }), comunas.map(function (c) { return c.count; }));

    if (data.turnaround.hasData) {
        bar('chart-turnaround', ['Días promedio'], [Math.round(data.turnaround.averageDays * 10) / 10], palette.accent);
    }

    donut('chart-sector', ['Archivo', 'Oficina 43'], [data.sector.archivo, data.sector.oficina43], [palette.muted, palette.accent]);

    bar('chart-f8-deadline', ['Dentro de plazo', 'Vencido'], [data.f8Deadline.withinDeadline, data.f8Deadline.pastDeadline], [palette.ok, palette.danger]);

    bar('chart-f8-pdf', ['Generado', 'Pendiente'], [data.f8Pdf.generated, data.f8Pdf.pending]);

    donut('chart-certificado-folder', ['Encontrada', 'No encontrada'], [data.certificadoFolder.found, data.certificadoFolder.notFound], [palette.ok, palette.danger]);

    donut('chart-certificado-notif', ['Notificado', 'Pendiente'], [data.certificadoNotif.notified, data.certificadoNotif.pending], [palette.ok, palette.pending]);

    var discarded = data.discarded || [];
    horizontalBar('chart-discarded', discarded.map(function (r) { return r.reason; }), discarded.map(function (r) { return r.count; }), palette.danger);
})();
