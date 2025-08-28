/**
 * Serilog Analytics Dashboard - Client-side functionality
 */

class SerilogDashboard {
    constructor() {
        this.apiBaseUrl = '/api/serilog-analytics';
        this.refreshInterval = null;
        this.charts = {};
        this.lastUpdate = null;
        
        // Pagination state
        this.currentPage = 1;
        this.pageSize = 10;
        this.totalRecords = 0;
        this.currentFilter = '';
        this.recentLogsData = [];
        
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

        // Recent logs controls
        $('#refreshLogsBtn').on('click', () => {
            this.loadRecentLogs();
        });

        // Filter dropdown - use event delegation
        $(document).on('click', '[data-level]', (e) => {
            e.preventDefault();
            const level = $(e.currentTarget).data('level');
            console.log('Filter selected:', level);
            this.currentFilter = level || '';
            this.currentPage = 1;
            
            // Update filter button text
            const filterText = level ? `${level} Only` : 'All Levels';
            $('.btn:contains("Filter")').html(`<i class="fas fa-filter"></i> ${filterText}`);
            
            this.loadRecentLogs();
        });

        // Page size dropdown - use event delegation
        $(document).on('click', '[data-page-size]', (e) => {
            e.preventDefault();
            const newPageSize = parseInt($(e.currentTarget).data('page-size'));
            console.log('Page size changed to:', newPageSize);
            this.pageSize = newPageSize;
            this.currentPage = 1;
            $('#pageSizeLabel').text(newPageSize);
            this.loadRecentLogs();
        });

        // Pagination clicks - use event delegation
        $(document).on('click', '#logsPagination .page-link', (e) => {
            e.preventDefault();
            const $link = $(e.currentTarget);
            const page = $link.data('page');
            
            console.log('Pagination clicked:', page, 'Current page:', this.currentPage);
            
            if (page === 'prev' && this.currentPage > 1) {
                this.currentPage--;
                this.loadRecentLogs();
            } else if (page === 'next') {
                const totalPages = Math.ceil(this.totalRecords / this.pageSize);
                if (this.currentPage < totalPages) {
                    this.currentPage++;
                    this.loadRecentLogs();
                }
            } else if (typeof page === 'number' && page !== this.currentPage) {
                this.currentPage = page;
                this.loadRecentLogs();
            }
        });

        // Copy log details
        $('#copyLogBtn').on('click', () => {
            this.copyLogDetails();
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
            this.updateRecentLogs(dashboardData.recentLogs);
            this.updatePerformanceMetrics(dashboardData.performance);
            
            // Load recent logs with pagination
            this.loadRecentLogs();
            
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
        // Update main statistics
        $('#totalLogs').text(this.formatNumber(data.statistics.totalLogs));
        $('#todayLogs').text(this.formatNumber(data.statistics.todayLogs));
        $('#errorCount').text(this.formatNumber(data.statistics.errorCount));
        $('#warningCount').text(this.formatNumber(data.statistics.warningCount));
        $('#infoCount').text(this.formatNumber(data.statistics.infoCount));
        
        // Update system health card
        const healthText = data.performance?.healthStatus || 'Unknown';
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
                healthIcon.addClass('bg-secondary');
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

        const totalCounts = hourlyData.map(item => item.totalCount || item.totalRequests || 0);
        const avgResponseTimes = hourlyData.map(item => item.avgResponseTime || 0);
        const errorCounts = hourlyData.map(item => item.errorCount || 0);

        this.charts.performance = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Total Logs',
                        data: totalCounts,
                        borderColor: 'rgb(75, 192, 192)',
                        backgroundColor: 'rgba(75, 192, 192, 0.1)',
                        tension: 0.4,
                        yAxisID: 'y'
                    },
                    {
                        label: 'Avg Response Time (ms)',
                        data: avgResponseTimes,
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
        $('#recentErrorCount').text(errors.length);

        if (errors.length === 0) {
            container.html(`
                <div class="text-center p-4">
                    <i class="fas fa-check-circle text-success" style="font-size: 2rem; margin-bottom: 1rem;"></i>
                    <div class="text-muted">No recent errors found</div>
                    <small class="text-success">System is running smoothly</small>
                </div>
            `);
            return;
        }

        let html = '';
        errors.slice(0, 8).forEach((error, index) => {
            const firstOccurrence = this.timeAgo(new Date(error.firstOccurrence));
            const lastOccurrence = this.timeAgo(new Date(error.lastOccurrence));
            const severity = this.getErrorSeverity(error.level);
            const affectedEndpoints = error.affectedEndpoints && error.affectedEndpoints.length > 0 
                ? error.affectedEndpoints.slice(0, 3).join(', ') 
                : 'N/A';

            html += `
                <div class="error-item mb-3" style="cursor: pointer;" data-bs-toggle="collapse" data-bs-target="#errorDetails${index}">
                    <div class="d-flex justify-content-between align-items-start">
                        <div class="flex-grow-1">
                            <div class="error-message d-flex align-items-center">
                                <i class="fas fa-exclamation-triangle text-${severity.color} me-2"></i>
                                <span class="fw-bold text-${severity.color}">${severity.label}</span>
                                <span class="ms-2">${this.truncateText(this.escapeHtml(error.errorMessage), 80)}</span>
                            </div>
                            <div class="error-summary mt-1">
                                <small class="text-muted">
                                    <i class="fas fa-clock me-1"></i>Last occurred: ${lastOccurrence}
                                    ${error.exceptionType ? `<span class="ms-2"><i class="fas fa-bug me-1"></i>${this.escapeHtml(error.exceptionType)}</span>` : ''}
                                </small>
                            </div>
                        </div>
                        <div class="text-end">
                            <span class="error-count">${error.count}</span>
                            <br><small class="text-muted">occurrences</small>
                        </div>
                    </div>
                    
                    <!-- Collapsible Details -->
                    <div class="collapse mt-2" id="errorDetails${index}">
                        <div class="card card-body bg-light border-0 mt-2">
                            <div class="row">
                                <div class="col-md-6">
                                    <strong>Full Error Message:</strong>
                                    <div class="text-break small mt-1 p-2 bg-white rounded border">
                                        ${this.escapeHtml(error.errorMessage)}
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="row">
                                        <div class="col-6">
                                            <strong>First Seen:</strong><br>
                                            <small class="text-muted">${firstOccurrence}</small>
                                        </div>
                                        <div class="col-6">
                                            <strong>Last Seen:</strong><br>
                                            <small class="text-muted">${lastOccurrence}</small>
                                        </div>
                                    </div>
                                    <div class="mt-2">
                                        <strong>Affected Endpoints:</strong><br>
                                        <small class="text-muted">${affectedEndpoints}</small>
                                    </div>
                                    ${error.exceptionType ? `
                                    <div class="mt-2">
                                        <strong>Exception Type:</strong><br>
                                        <code class="small">${this.escapeHtml(error.exceptionType)}</code>
                                    </div>
                                    ` : ''}
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            `;
        });

        if (errors.length > 8) {
            html += `
                <div class="text-center mt-3">
                    <small class="text-muted">Showing 8 of ${errors.length} errors</small>
                </div>
            `;
        }

        container.html(html);
    }

    getErrorSeverity(level) {
        switch (level?.toLowerCase()) {
            case 'fatal':
                return { label: 'FATAL', color: 'danger' };
            case 'error':
                return { label: 'ERROR', color: 'danger' };
            case 'warning':
                return { label: 'WARN', color: 'warning' };
            default:
                return { label: 'ERROR', color: 'danger' };
        }
    }

    truncateText(text, maxLength) {
        if (text.length <= maxLength) return text;
        return text.substring(0, maxLength) + '...';
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

    setupAutoRefresh(intervalInSeconds = 0) {
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

    async loadRecentLogs() {
        try {
            // Show loading state
            const tbody = $('#recentLogsTableBody');
            tbody.html(`
                <tr>
                    <td colspan="8" class="text-center p-4">
                        <div class="d-flex flex-column align-items-center">
                            <i class="fas fa-spinner fa-spin fa-2x text-primary mb-2"></i>
                            <span class="text-muted">Loading logs...</span>
                        </div>
                    </td>
                </tr>
            `);

            const requestBody = {
                page: this.currentPage,
                pageSize: this.pageSize
            };

            // Add level filter if set
            if (this.currentFilter) {
                requestBody.level = this.currentFilter;
            }

            console.log('Loading recent logs with params:', requestBody);

            const response = await fetch(`${this.apiBaseUrl}/search`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val(),
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify(requestBody)
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const data = await response.json();
            console.log('Received logs data:', data);
            
            this.recentLogsData = data.items || [];
            this.totalRecords = data.totalCount || 0;
            
            this.updateRecentLogsTable();
            this.updatePagination();
            
        } catch (error) {
            console.error('Error loading recent logs:', error);
            const tbody = $('#recentLogsTableBody');
            tbody.html(`
                <tr>
                    <td colspan="8" class="text-center p-4">
                        <div class="d-flex flex-column align-items-center">
                            <i class="fas fa-exclamation-triangle fa-2x text-danger mb-2"></i>
                            <span class="text-danger">Failed to load logs: ${error.message}</span>
                            <button class="btn btn-sm btn-primary mt-2" onclick="window.serilogDashboard.loadRecentLogs()">
                                <i class="fas fa-retry"></i> Retry
                            </button>
                        </div>
                    </td>
                </tr>
            `);
        }
    }

    updateRecentLogsTable() {
        const tbody = $('#recentLogsTableBody');
        tbody.empty();

        if (!this.recentLogsData || this.recentLogsData.length === 0) {
            tbody.append(`
                <tr>
                    <td colspan="8" class="text-center p-4">
                        <div class="d-flex flex-column align-items-center">
                            <i class="fas fa-info-circle fa-2x text-muted mb-2"></i>
                            <span class="text-muted">No logs found for the current filter</span>
                        </div>
                    </td>
                </tr>
            `);
            return;
        }

        this.recentLogsData.forEach((log, index) => {
            const timestamp = new Date(log.timeStamp).toLocaleString();
            const levelBadge = this.getLevelBadge(log.level);
            const messagePreview = log.message ? (log.message.length > 80 ? 
                log.message.substring(0, 80) + '...' : log.message) : '';
            const hasException = log.hasException ? 
                '<i class="fas fa-exclamation-triangle text-danger" title="Has Exception"></i>' : 
                '<i class="fas fa-check text-success" title="No Exception"></i>';
            const userId = log.userId ? log.userId.substring(0, 8) + '...' : '-';
            const requestPath = log.requestPath || '-';
            const pathDisplay = requestPath.length > 25 ? requestPath.substring(0, 25) + '...' : requestPath;

            tbody.append(`
                <tr class="log-row" data-index="${index}">
                    <td class="text-nowrap">
                        <small>${timestamp}</small>
                    </td>
                    <td class="text-center">${levelBadge}</td>
                    <td>
                        <small class="text-muted">${log.application || '-'}</small>
                    </td>
                    <td>
                        <small title="${requestPath}" class="text-primary">${pathDisplay}</small>
                    </td>
                    <td>
                        <span class="d-inline-block text-truncate" style="max-width: 400px;" title="${log.message || ''}">${messagePreview}</span>
                    </td>
                    <td class="text-center">
                        <small class="text-muted">${userId}</small>
                    </td>
                    <td class="text-center">${hasException}</td>
                    <td class="text-center">
                        <button type="button" class="btn btn-sm btn-outline-primary view-log-btn" data-index="${index}" title="View Details">
                            <i class="fas fa-eye"></i>
                        </button>
                    </td>
                </tr>
            `);
        });

        // Add click handlers for view buttons
        $('.view-log-btn').on('click', (e) => {
            const index = $(e.currentTarget).data('index');
            this.showLogDetail(this.recentLogsData[index]);
        });

        // Update count badge
        $('#logsCountBadge').text(this.totalRecords);
    }

    updateRecentLogs(recentLogs) {
        // Legacy function - now redirects to new method
        this.recentLogsData = recentLogs || [];
        this.totalRecords = this.recentLogsData.length;
        this.updateRecentLogsTable();
    }

    updatePerformanceMetrics(performance) {
        if (!performance) return;

        $('#requestsPerMinute').text(performance.requestsPerMinute || 0);
        $('#errorsPerMinute').text(performance.errorsPerMinute || 0);
        
        const errorRate = performance.additionalMetrics?.ErrorRate || 0;
        const successRate = performance.additionalMetrics?.SuccessRate || 0;
        const totalRequests = performance.additionalMetrics?.TotalRequests || 0;
        
        $('#errorRate').text(errorRate.toFixed(1) + '%');
        $('#successRate').text(successRate.toFixed(1) + '%');
        $('#totalRequests').text(this.formatNumber(totalRequests));
    }

    updatePagination() {
        const totalPages = Math.ceil(this.totalRecords / this.pageSize) || 1;
        const pagination = $('#logsPagination');
        const recordsInfo = $('#recordsInfo');
        
        // Update records info
        if (this.totalRecords === 0) {
            recordsInfo.text('0 - 0 of 0 records');
        } else {
            const startRecord = ((this.currentPage - 1) * this.pageSize) + 1;
            const endRecord = Math.min(this.currentPage * this.pageSize, this.totalRecords);
            recordsInfo.text(`${startRecord} - ${endRecord} of ${this.totalRecords} records`);
        }
        
        // Clear existing pagination
        pagination.empty();
        
        // If no records, don't show pagination
        if (this.totalRecords === 0) {
            return;
        }
        
        // Previous button
        const prevDisabled = this.currentPage <= 1;
        pagination.append(`
            <li class="page-item ${prevDisabled ? 'disabled' : ''}">
                <a class="page-link" href="#" data-page="prev" ${prevDisabled ? 'tabindex="-1"' : ''}>
                    <i class="fas fa-chevron-left"></i> Previous
                </a>
            </li>
        `);
        
        // Show page numbers only if there are multiple pages
        if (totalPages > 1) {
            // Page numbers logic
            let startPage = Math.max(1, this.currentPage - 2);
            let endPage = Math.min(totalPages, startPage + 4);
            
            // Adjust start page if we're near the end
            if (endPage - startPage < 4) {
                startPage = Math.max(1, endPage - 4);
            }
            
            // Show first page and ellipsis if needed
            if (startPage > 1) {
                pagination.append(`
                    <li class="page-item">
                        <a class="page-link" href="#" data-page="1">1</a>
                    </li>
                `);
                if (startPage > 2) {
                    pagination.append(`
                        <li class="page-item disabled">
                            <span class="page-link">...</span>
                        </li>
                    `);
                }
            }
            
            // Show page numbers in range
            for (let i = startPage; i <= endPage; i++) {
                const activeClass = i === this.currentPage ? 'active' : '';
                pagination.append(`
                    <li class="page-item ${activeClass}">
                        <a class="page-link" href="#" data-page="${i}">${i}</a>
                    </li>
                `);
            }
            
            // Show ellipsis and last page if needed
            if (endPage < totalPages) {
                if (endPage < totalPages - 1) {
                    pagination.append(`
                        <li class="page-item disabled">
                            <span class="page-link">...</span>
                        </li>
                    `);
                }
                pagination.append(`
                    <li class="page-item">
                        <a class="page-link" href="#" data-page="${totalPages}">${totalPages}</a>
                    </li>
                `);
            }
        }
        
        // Next button
        const nextDisabled = this.currentPage >= totalPages;
        pagination.append(`
            <li class="page-item ${nextDisabled ? 'disabled' : ''}">
                <a class="page-link" href="#" data-page="next" ${nextDisabled ? 'tabindex="-1"' : ''}>
                    Next <i class="fas fa-chevron-right"></i>
                </a>
            </li>
        `);
        
        console.log(`Pagination updated: Page ${this.currentPage} of ${totalPages}, Total records: ${this.totalRecords}`);
    }

    showLogDetail(log) {
        // Populate modal with log details
        $('#modalTimestamp').text(new Date(log.timeStamp).toLocaleString());
        $('#modalLevel').html(this.getLevelBadge(log.level));
        $('#modalApplication').text(log.application || 'N/A');
        $('#modalPath').text(log.requestPath || 'N/A');
        $('#modalUserId').text(log.userId || 'N/A');
        $('#modalHttpMethod').text(log.httpMethod || 'N/A');
        $('#modalDuration').text(log.duration ? log.duration + 'ms' : 'N/A');
        $('#modalResponseStatus').text(log.responseStatusCode || 'N/A');
        $('#modalMessage').text(log.message || 'No message available');
        
        // Handle exception
        if (log.exception) {
            $('#modalException').text(log.exception);
            $('#modalExceptionContainer').show();
        } else {
            $('#modalExceptionContainer').hide();
        }
        
        // Store log data for copying
        this.currentLogDetail = log;
        
        // Show modal
        const modal = new bootstrap.Modal(document.getElementById('logDetailModal'));
        modal.show();
    }

    copyLogDetails() {
        if (!this.currentLogDetail) return;
        
        const log = this.currentLogDetail;
        const details = `
Log Entry Details:
==================
Timestamp: ${new Date(log.timeStamp).toLocaleString()}
Level: ${log.level}
Application: ${log.application || 'N/A'}
Request Path: ${log.requestPath || 'N/A'}
User ID: ${log.userId || 'N/A'}
HTTP Method: ${log.httpMethod || 'N/A'}
Duration: ${log.duration ? log.duration + 'ms' : 'N/A'}
Response Status: ${log.responseStatusCode || 'N/A'}

Message:
--------
${log.message || 'No message available'}

${log.exception ? 'Exception:\n----------\n' + log.exception : ''}
        `.trim();
        
        navigator.clipboard.writeText(details).then(() => {
            this.showSuccess('Log details copied to clipboard');
        }).catch(err => {
            console.error('Failed to copy: ', err);
            this.showError('Failed to copy to clipboard');
        });
    }

    getLevelBadge(level) {
        const levelMap = {
            'Information': 'badge bg-info',
            'Warning': 'badge bg-warning',
            'Error': 'badge bg-danger',
            'Fatal': 'badge bg-dark',
            'Debug': 'badge bg-secondary',
            'Verbose': 'badge bg-light text-dark'
        };
        const badgeClass = levelMap[level] || 'badge bg-secondary';
        return `<span class="${badgeClass}">${level}</span>`;
    }
}

// Initialize dashboard when document is ready
$(document).ready(() => {
    window.serilogDashboard = new SerilogDashboard();
});