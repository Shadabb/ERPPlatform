/**
 * Serilog Analytics Dashboard - Client-side functionality
 */

class SerilogDashboard {
    constructor() {
        this.apiBaseUrl = '/api/serilog-analytics';
        this.refreshInterval = null;
        this.charts = {};
        this.lastUpdate = null;
        
        this.init();
    }

    init() {
        this.setupEventHandlers();
        this.initializeDateInputs();
        this.loadDashboard();
        this.setupAutoRefresh();
    }

    setupEventHandlers() {
        // Refresh button
        $('#refreshBtn').on('click', () => {
            this.loadDashboard();
        });

        // Export button
        $('#exportBtn').on('click', () => {
            this.exportData();
        });

        // Auto refresh dropdown items
        $('[data-refresh]').on('click', (e) => {
            e.preventDefault();
            const interval = $(e.target).data('refresh');
            this.setupAutoRefresh(interval);
        });

        // Date inputs
        $('#fromDate, #toDate').on('change', () => {
            this.loadDashboard();
        });

        // Advanced search
        $('#performSearch').on('click', () => {
            this.performAdvancedSearch();
        });
    }

    initializeDateInputs() {
        const now = new Date();
        const yesterday = new Date(now.getTime() - 24 * 60 * 60 * 1000);
        
        $('#fromDate').val(this.formatDateForInput(yesterday));
        $('#toDate').val(this.formatDateForInput(now));
    }

    formatDateForInput(date) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        const hours = String(date.getHours()).padStart(2, '0');
        const minutes = String(date.getMinutes()).padStart(2, '0');
        
        return `${year}-${month}-${day}T${hours}:${minutes}`;
    }

    async loadDashboard() {
        try {
            this.showLoadingIndicator();
            
            const fromDate = $('#fromDate').val();
            const toDate = $('#toDate').val();
            
            // Load dashboard data
            const dashboardData = await this.fetchDashboardData(fromDate, toDate);
            
            // Debug logging
            console.log('Dashboard data received:', dashboardData);
            console.log('TopErrors:', dashboardData.topErrors);
            console.log('SlowRequests:', dashboardData.slowRequests);
            console.log('TopEndpoints:', dashboardData.topEndpoints);

            // Update UI components
            this.updateSummaryCards(dashboardData);
            this.updatePerformanceChart(dashboardData.hourlyTrends);
            this.updateLogLevelChart(dashboardData.logLevelDistribution);
            this.updateRecentErrors(dashboardData.topErrors);
            this.updateSlowRequests(dashboardData.slowRequests);
            this.updateEndpointsTable(dashboardData.topEndpoints);
            
            this.lastUpdate = new Date();
            this.hideLoadingIndicator();
            this.showUpdateNotification('Dashboard updated successfully');
            
        } catch (error) {
            console.error('Error loading dashboard:', error);
            console.error('API URL attempted:', `${this.apiBaseUrl}/dashboard`);
            console.error('Request details:', {
                fromDate: fromDate,
                toDate: toDate,
                requestBody: JSON.stringify({
                    fromDate: fromDate ? new Date(fromDate).toISOString() : null,
                    toDate: toDate ? new Date(toDate).toISOString() : null
                })
            });
            this.showError(`Failed to load dashboard data: ${error.message}`);
            this.hideLoadingIndicator();
        }
    }

    async fetchDashboardData(fromDate, toDate) {
        const headers = {
            'Content-Type': 'application/json',
        };
        
        // Add CSRF token if available
        const token = $('input[name="__RequestVerificationToken"]').val();
        if (token) {
            headers['X-CSRF-TOKEN'] = token;
            headers['RequestVerificationToken'] = token;
        }

        const response = await fetch(`${this.apiBaseUrl}/dashboard`, {
            method: 'POST',
            headers: headers,
            credentials: 'include', // Include cookies for authentication
            body: JSON.stringify({
                fromDate: fromDate ? new Date(fromDate).toISOString() : null,
                toDate: toDate ? new Date(toDate).toISOString() : null
            })
        });

        if (!response.ok) {
            const errorText = await response.text();
            console.error('Response error details:', {
                status: response.status,
                statusText: response.statusText,
                body: errorText,
                url: response.url
            });
            throw new Error(`HTTP ${response.status}: ${response.statusText} - ${errorText}`);
        }

        return await response.json();
    }

    updateSummaryCards(data) {
        $('#totalLogs').text(this.formatNumber(data.statistics.totalLogs));
        $('#avgResponseTime').text(Math.round(data.statistics.avgResponseTime));
        $('#errorRate').text(data.statistics.errorRate.toFixed(2));
        
        // Update system health card
        const healthText = data.performance.healthStatus;
        $('#systemHealth').text(healthText);
        
        // Update health card icon color based on status
        const healthIcon = $('#healthCard .metric-icon');
        healthIcon.removeClass('bg-success bg-warning bg-danger bg-secondary bg-info');
        switch (healthText.toLowerCase()) {
            case 'healthy':
                healthIcon.addClass('bg-success');
                break;
            case 'warning':
                healthIcon.addClass('bg-warning');
                break;
            case 'critical':
                healthIcon.addClass('bg-danger');
                break;
            default:
                healthIcon.addClass('bg-info');
        }
    }

    updatePerformanceChart(hourlyData) {
        const ctx = document.getElementById('performanceChart').getContext('2d');
        
        if (this.charts.performance) {
            this.charts.performance.destroy();
        }

        const labels = hourlyData.map(item => {
            const date = new Date(item.hour);
            return date.getHours().toString().padStart(2, '0') + ':00';
        });

        const requestCounts = hourlyData.map(item => item.requestCount);
        const avgDurations = hourlyData.map(item => item.avgDuration);
        const errorCounts = hourlyData.map(item => item.errorCount);

        this.charts.performance = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Request Count',
                        data: requestCounts,
                        borderColor: 'rgb(75, 192, 192)',
                        backgroundColor: 'rgba(75, 192, 192, 0.1)',
                        tension: 0.4,
                        yAxisID: 'y'
                    },
                    {
                        label: 'Avg Duration (ms)',
                        data: avgDurations,
                        borderColor: 'rgb(255, 99, 132)',
                        backgroundColor: 'rgba(255, 99, 132, 0.1)',
                        tension: 0.4,
                        yAxisID: 'y1'
                    },
                    {
                        label: 'Error Count',
                        data: errorCounts,
                        borderColor: 'rgb(255, 205, 86)',
                        backgroundColor: 'rgba(255, 205, 86, 0.1)',
                        tension: 0.4,
                        yAxisID: 'y'
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false,
                },
                scales: {
                    x: {
                        display: true,
                        title: {
                            display: true,
                            text: 'Hour'
                        }
                    },
                    y: {
                        type: 'linear',
                        display: true,
                        position: 'left',
                        title: {
                            display: true,
                            text: 'Count'
                        }
                    },
                    y1: {
                        type: 'linear',
                        display: true,
                        position: 'right',
                        title: {
                            display: true,
                            text: 'Duration (ms)'
                        },
                        grid: {
                            drawOnChartArea: false,
                        },
                    }
                }
            }
        });
    }

    updateLogLevelChart(logLevelData) {
        const ctx = document.getElementById('logLevelChart').getContext('2d');
        
        if (this.charts.logLevel) {
            this.charts.logLevel.destroy();
        }

        const labels = logLevelData.map(item => item.level);
        const counts = logLevelData.map(item => item.count);
        const colors = {
            'Information': 'rgba(54, 162, 235, 0.8)',
            'Warning': 'rgba(255, 206, 86, 0.8)',
            'Error': 'rgba(255, 99, 132, 0.8)',
            'Fatal': 'rgba(153, 102, 255, 0.8)'
        };

        this.charts.logLevel = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: counts,
                    backgroundColor: labels.map(label => colors[label] || 'rgba(201, 203, 207, 0.8)')
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                    }
                }
            }
        });
    }

    updateRecentErrors(errors) {
        const container = $('#recentErrorsContainer');
        $('#errorCount').text(errors.length);

        if (errors.length === 0) {
            container.html('<div class="text-center p-3 text-muted">No recent errors found</div>');
            return;
        }

        let html = '';
        errors.slice(0, 5).forEach(error => {
            const timeAgo = this.timeAgo(new Date(error.lastOccurrence));
            html += `
                <div class="error-item">
                    <div class="error-message">${this.escapeHtml(error.errorMessage)}</div>
                    <div class="error-details">
                        <span class="error-count">${error.count}</span>
                        <small class="text-muted ml-2">${timeAgo}</small>
                        ${error.exceptionType ? `<br><small class="text-muted">${this.escapeHtml(error.exceptionType)}</small>` : ''}
                    </div>
                </div>
            `;
        });

        container.html(html);
    }

    updateSlowRequests(slowRequests) {
        const container = $('#slowRequestsContainer');
        $('#slowRequestCount').text(slowRequests.length);

        if (slowRequests.length === 0) {
            container.html('<div class="text-center p-3 text-muted">No slow requests found</div>');
            return;
        }

        let html = '';
        slowRequests.slice(0, 5).forEach(request => {
            const timeAgo = this.timeAgo(new Date(request.timeStamp));
            html += `
                <div class="slow-request-item">
                    <div class="request-path">${this.escapeHtml(request.requestPath)}</div>
                    <div class="request-details">
                        <span class="duration-badge">${request.duration}ms</span>
                        <small class="text-muted ml-2">${timeAgo}</small>
                        ${request.userId ? `<br><small class="text-muted">User: ${this.escapeHtml(request.userId)}</small>` : ''}
                    </div>
                </div>
            `;
        });

        container.html(html);
    }

    updateEndpointsTable(endpoints) {
        const tbody = $('#endpointsTableBody');

        if (endpoints.length === 0) {
            tbody.html('<tr><td colspan="6" class="text-center text-muted">No endpoint data available</td></tr>');
            return;
        }

        let html = '';
        endpoints.forEach(endpoint => {
            const errorRate = endpoint.requestCount > 0 ? 
                ((endpoint.errorCount / endpoint.requestCount) * 100).toFixed(1) : '0.0';
            
            const statusClass = this.getPerformanceStatusClass(endpoint.avgDuration);
            const statusText = this.getPerformanceStatusText(endpoint.avgDuration);

            html += `
                <tr>
                    <td><code>${this.escapeHtml(endpoint.endpoint)}</code></td>
                    <td>${this.formatNumber(endpoint.requestCount)}</td>
                    <td>${Math.round(endpoint.avgDuration)}ms</td>
                    <td>${Math.round(endpoint.maxDuration)}ms</td>
                    <td>${errorRate}%</td>
                    <td><span class="status-badge ${statusClass}">${statusText}</span></td>
                </tr>
            `;
        });

        tbody.html(html);
    }

    getPerformanceStatusClass(duration) {
        if (duration <= 100) return 'status-excellent';
        if (duration <= 500) return 'status-good';
        if (duration <= 1000) return 'status-fair';
        if (duration <= 5000) return 'status-slow';
        return 'status-critical';
    }

    getPerformanceStatusText(duration) {
        if (duration <= 100) return 'Excellent';
        if (duration <= 500) return 'Good';
        if (duration <= 1000) return 'Fair';
        if (duration <= 5000) return 'Slow';
        return 'Critical';
    }

    setupAutoRefresh(intervalInSeconds = 60) {
        if (this.refreshInterval) {
            clearInterval(this.refreshInterval);
        }

        const interval = parseInt(intervalInSeconds) * 1000;
        
        if (interval > 0) {
            this.refreshInterval = setInterval(() => {
                this.loadDashboard();
            }, interval);
        }
    }

    async exportData() {
        try {
            const fromDate = $('#fromDate').val();
            const toDate = $('#toDate').val();
            
            const headers = {
                'Content-Type': 'application/json',
            };
            
            // Add CSRF token if available
            const token = $('input[name="__RequestVerificationToken"]').val();
            if (token) {
                headers['X-CSRF-TOKEN'] = token;
                headers['RequestVerificationToken'] = token;
            }
            
            const response = await fetch(`${this.apiBaseUrl}/export/csv`, {
                method: 'POST',
                headers: headers,
                credentials: 'include',
                body: JSON.stringify({
                    fromDate: fromDate ? new Date(fromDate).toISOString() : null,
                    toDate: toDate ? new Date(toDate).toISOString() : null,
                    pageSize: 10000
                })
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const blob = await response.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.style.display = 'none';
            a.href = url;
            a.download = `serilog-analytics-${new Date().toISOString().split('T')[0]}.csv`;
            document.body.appendChild(a);
            a.click();
            window.URL.revokeObjectURL(url);
            document.body.removeChild(a);

            this.showSuccess('Data exported successfully');
        } catch (error) {
            console.error('Error exporting data:', error);
            this.showError('Failed to export data');
        }
    }

    async performAdvancedSearch() {
        // Implementation for advanced search functionality
        $('#searchModal').modal('hide');
        // Add search logic here based on form inputs
    }

    // Utility methods
    formatNumber(num) {
        if (num >= 1000000) {
            return (num / 1000000).toFixed(1) + 'M';
        }
        if (num >= 1000) {
            return (num / 1000).toFixed(1) + 'K';
        }
        return num.toString();
    }

    timeAgo(date) {
        const seconds = Math.floor((new Date() - date) / 1000);
        
        if (seconds < 60) return `${seconds} seconds ago`;
        if (seconds < 3600) return `${Math.floor(seconds / 60)} minutes ago`;
        if (seconds < 86400) return `${Math.floor(seconds / 3600)} hours ago`;
        return `${Math.floor(seconds / 86400)} days ago`;
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    showLoadingIndicator() {
        // Add loading states to cards
        $('#summaryCards .small-box').addClass('loading');
    }

    hideLoadingIndicator() {
        $('#summaryCards .small-box').removeClass('loading');
    }

    showUpdateNotification(message) {
        // Show brief success notification
        const indicator = $(`<div class="update-indicator">${message}</div>`);
        $('body').append(indicator);
        indicator.addClass('show');
        
        setTimeout(() => {
            indicator.removeClass('show');
            setTimeout(() => indicator.remove(), 300);
        }, 2000);
    }

    showSuccess(message) {
        // Integration with ABP notification system or custom toast
        console.log('Success:', message);
    }

    showError(message) {
        // Integration with ABP notification system or custom toast
        console.error('Error:', message);
    }
}

// Initialize dashboard when document is ready
$(document).ready(() => {
    window.serilogDashboard = new SerilogDashboard();
});