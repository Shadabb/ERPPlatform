// Log Analytics Dashboard JavaScript
class LogAnalyticsDashboard {
    constructor() {
        this.charts = {};
        this.autoRefreshInterval = null;
        this.isAutoRefreshing = false;
        this.refreshIntervalMs = 30000; // 30 seconds
        this.connection = null;
        
        // Pagination data storage (server-side pagination for logs)
        this.paginationData = {
            recentLogs: { data: [], currentSkip: 0, take: 10, totalCount: 0, hasMore: false },
            recentAuditLogs: { data: [], currentSkip: 0, take: 10, totalCount: 0, hasMore: false },
            // Client-side pagination for smaller datasets
            topErrors: { data: [], currentPage: 1, itemsPerPage: 5 },
            performance: { data: [], currentPage: 1, itemsPerPage: 5 },
            topUserActivities: { data: [], currentPage: 1, itemsPerPage: 5 },
            apiEndpointPerformance: { data: [], currentPage: 1, itemsPerPage: 5 }
        };
        
        // Audit search pagination state
        this.auditSearchState = {
            currentPage: 1,
            pageSize: 20,
            totalCount: 0,
            totalPages: 0,
            searchRequest: null
        };
        
        // Search locks to prevent concurrent execution
        this.auditSearchInProgress = false;
        this.logSearchInProgress = false;
        this.paginationInProgress = false;
        
        // Recent Logs search context for pagination
        this.recentLogsSearchContext = {
            searchRequest: null,
            isActive: false
        };
        
        this.init();
    }

    async init() {
        try {
            console.log('=== Dashboard Initialization Starting ===');
            
            // Check if required libraries are loaded
            console.log('Checking dependencies...');
            console.log('Chart.js available:', typeof window.Chart !== 'undefined');
            console.log('SignalR available:', typeof signalR !== 'undefined');
            console.log('Bootstrap available:', typeof bootstrap !== 'undefined');
            
            // Make SignalR optional - don't let it block dashboard initialization
            this.setupSignalRConnection().catch(error => {
                console.warn('SignalR connection failed, continuing without real-time updates:', error);
            });
            
            await this.loadApplications();
            await this.loadDashboardData();
            this.setupEventHandlers();
            this.setupDateDefaults();
            
            console.log('=== Dashboard Initialization Complete ===');
        } catch (error) {
            console.error('Failed to initialize dashboard:', error);
            this.showError('Failed to load dashboard data');
        }
    }

    async setupSignalRConnection() {
        try {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl("/log-analytics-hub")
                .build();

            this.connection.on("DashboardUpdated", (data) => {
                this.updateMetrics(data.statistics);
                this.updateCharts(data);
                this.updateRecentLogs(data.recentLogs);
                this.updateTopErrors(data.topErrors);
                this.updatePerformanceMetrics(data.performanceMetrics);
                this.updateAuditMetrics(data.auditStatistics);
                this.updateRecentAuditLogs(data.recentAuditLogs);
                this.updateSystemHealth(data.statistics);
                this.updateApiEndpoints(data.auditStatistics);
                this.updateApiEndpointPerformance(data.auditStatistics);
            });

            this.connection.on("NewLogEntry", (logEntry) => {
                this.prependLogEntry(logEntry);
            });

            await this.connection.start();
            await this.connection.invoke("JoinDashboardGroup");
            
            console.log('SignalR connected successfully');
        } catch (error) {
            console.error('SignalR connection failed:', error);
        }
    }

    prependLogEntry(logEntry) {
        // Add new log entry to the beginning of the pagination data
        this.paginationData.recentLogs.data.unshift(logEntry);
        
        // Keep only the most recent entries (limit to reasonable number for performance)
        if (this.paginationData.recentLogs.data.length > 100) {
            this.paginationData.recentLogs.data = this.paginationData.recentLogs.data.slice(0, 100);
        }
        
        // Reset to first page to show the newest entry
        this.paginationData.recentLogs.currentPage = 1;
        
        // Re-render the paginated content
        this.renderPaginatedContent('recentLogs');
    }

    setupEventHandlers() {
        // Auto-refresh toggle
        window.toggleAutoRefresh = () => this.toggleAutoRefresh();
        window.refreshDashboard = () => this.loadDashboardData();
        window.exportLogs = () => this.showExportDialog();
        window.showLogSearch = () => this.showLogSearch();
        window.searchLogs = () => this.searchLogs();
        window.showAuditLogSearch = () => this.showAuditLogSearch();
        window.searchAuditLogs = () => this.searchAuditLogs();
        window.previousAuditSearchPage = () => this.previousAuditSearchPage();
        window.nextAuditSearchPage = () => this.nextAuditSearchPage();
        window.validateAuditDateRange = () => this.validateAuditDateRange();
        window.resetAuditLogSearchForm = () => this.resetAuditLogSearchForm();
        
        // Log Search form controls
        window.resetLogSearchForm = () => this.resetLogSearchForm();
        window.validateDateRange = () => this.validateDateRange();
        
        // Pagination handlers
        window.previousRecentLogs = () => this.previousPage('recentLogs');
        window.nextRecentLogs = () => this.nextPage('recentLogs');
        window.previousRecentAuditLogs = () => this.previousPage('recentAuditLogs');
        window.nextRecentAuditLogs = () => this.nextPage('recentAuditLogs');
        window.previousTopErrors = () => this.previousPage('topErrors');
        window.nextTopErrors = () => this.nextPage('topErrors');
        window.previousPerformance = () => this.previousPage('performance');
        window.nextPerformance = () => this.nextPage('performance');
        window.previousTopUserActivities = () => this.previousPage('topUserActivities');
        window.nextTopUserActivities = () => this.nextPage('topUserActivities');
    }

    setupDateDefaults() {
        const now = new Date();
        const yesterday = new Date(now.getTime() - (24 * 60 * 60 * 1000));
        
        // Set default values for Log Search
        document.getElementById('fromDate').value = this.formatDateTimeLocal(yesterday);
        document.getElementById('toDate').value = this.formatDateTimeLocal(now);
        
        // Set max attributes to prevent future dates
        const maxDateTime = new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
        const maxDate = now.getFullYear() + '-' + 
                       String(now.getMonth() + 1).padStart(2, '0') + '-' + 
                       String(now.getDate()).padStart(2, '0');
        
        // Set max for Log Search (datetime-local)
        const fromDateEl = document.getElementById('fromDate');
        const toDateEl = document.getElementById('toDate');
        if (fromDateEl) {
            fromDateEl.setAttribute('max', maxDateTime);
            fromDateEl.max = maxDateTime;
        }
        if (toDateEl) {
            toDateEl.setAttribute('max', maxDateTime);
            toDateEl.max = maxDateTime;
        }
        
        // Set max for Audit Log Search (date)
        const auditFromDateEl = document.getElementById('auditFromDate');
        const auditToDateEl = document.getElementById('auditToDate');
        if (auditFromDateEl) {
            auditFromDateEl.setAttribute('max', maxDate);
            auditFromDateEl.max = maxDate;
        }
        if (auditToDateEl) {
            auditToDateEl.setAttribute('max', maxDate);
            auditToDateEl.max = maxDate;
        }
        
        console.log('Date constraints set - maxDateTime:', maxDateTime, 'maxDate:', maxDate);
    }

    formatDateTimeLocal(date) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        const hours = String(date.getHours()).padStart(2, '0');
        const minutes = String(date.getMinutes()).padStart(2, '0');
        
        return `${year}-${month}-${day}T${hours}:${minutes}`;
    }

    async loadApplications() {
        try {
            const response = await fetch('/api/log-analytics/applications');
            const result = await response.json();
            
            console.log('Applications data:', result);
            
            const select = document.getElementById('applications');
            select.innerHTML = '';
            
            // The API returns {applications: [...], lastUpdated: "..."}
            if (result.applications && Array.isArray(result.applications)) {
                result.applications.forEach(app => {
                    const option = document.createElement('option');
                    option.value = app;
                    option.textContent = app;
                    select.appendChild(option);
                });
                console.log(`Loaded ${result.applications.length} applications`);
            } else {
                console.warn('No applications found in response:', result);
            }
        } catch (error) {
            console.error('Failed to load applications:', error);
        }
    }

    async loadDashboardData() {
        try {
            console.log('Loading dashboard data...');
            this.showLoadingState();
            
            // Disabled container test - using real pagination now
            // console.log('Testing direct container update...');
            // this.testContainerUpdates();
            
            const response = await fetch('/api/log-analytics/dashboard');
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            
            const data = await response.json();
            console.log('Dashboard data loaded:', data);
            
            // Clear all loading states before updating with real data
            this.clearLoadingState();
            
            this.updateMetrics(data.statistics);
            this.updateCharts(data);
            this.updateRecentLogs(data.recentLogs);
            this.updateTopErrors(data.topErrors);
            this.updatePerformanceMetrics(data.performanceMetrics);
            this.updateTopUserActivities(data.topUserActivities);
            this.updateAuditMetrics(data.auditStatistics);
            this.updateRecentAuditLogs(data.recentAuditLogs);
            this.updateSystemHealth(data.statistics);
            this.updateApiEndpoints(data.auditStatistics);
            this.updateApiEndpointPerformance(data.auditStatistics);
            
            await this.checkSystemHealth();
            console.log('Dashboard updated successfully');
            
        } catch (error) {
            console.error('Failed to load dashboard data:', error);
            this.showError('Failed to load dashboard data: ' + error.message);
        }
    }

    testContainerUpdates() {
        console.log('Running container update test...');
        
        // Test recent logs container
        const recentLogsContainer = document.getElementById('recentLogsContainer');
        if (recentLogsContainer) {
            console.log('Found recentLogsContainer, updating with test data');
            recentLogsContainer.innerHTML = '<div class="p-3 bg-success text-white">TEST: Recent Logs Container Working!</div>';
        } else {
            console.error('recentLogsContainer not found!');
        }
        
        // Test audit logs container
        const auditLogsContainer = document.getElementById('recentAuditLogsContainer');
        if (auditLogsContainer) {
            console.log('Found recentAuditLogsContainer, updating with test data');
            auditLogsContainer.innerHTML = '<div class="p-3 bg-info text-white">TEST: Audit Logs Container Working!</div>';
        } else {
            console.error('recentAuditLogsContainer not found!');
        }
        
        // Test top errors container
        const topErrorsContainer = document.getElementById('topErrorsContainer');
        if (topErrorsContainer) {
            console.log('Found topErrorsContainer, updating with test data');
            topErrorsContainer.innerHTML = '<div class="p-3 bg-warning text-dark">TEST: Top Errors Container Working!</div>';
        } else {
            console.error('topErrorsContainer not found!');
        }
        
        // Test user activities container
        const userActivitiesContainer = document.getElementById('topUserActivitiesContainer');
        if (userActivitiesContainer) {
            console.log('Found topUserActivitiesContainer, updating with test data');
            userActivitiesContainer.innerHTML = '<div class="p-3 bg-primary text-white">TEST: User Activities Container Working!</div>';
        } else {
            console.error('topUserActivitiesContainer not found!');
        }
        
        
        console.log('Container test completed');
    }

    showLoadingState() {
        // Show loading spinners for main sections
        const containers = ['recentLogsContainer', 'topErrorsContainer', 'performanceContainer', 'recentAuditLogsContainer', 'topUserActivitiesContainer'];
        console.log('Showing loading state for containers:', containers);
        
        containers.forEach(containerId => {
            const container = document.getElementById(containerId);
            if (container) {
                container.innerHTML = `
                    <div class="loading-state text-center p-4">
                        <div class="spinner-border text-primary" role="status">
                            <span class="visually-hidden">Loading...</span>
                        </div>
                    </div>
                `;
                console.log(`Loading state shown for: ${containerId}`);
            } else {
                console.warn(`Container not found: ${containerId}`);
            }
        });
    }

    clearLoadingState() {
        // Remove all loading states
        console.log('Clearing all loading states...');
        const loadingElements = document.querySelectorAll('.loading-state');
        loadingElements.forEach(el => {
            console.log('Removing loading state element');
            el.remove();
        });
    }

    updateMetrics(statistics) {
        console.log('Updating metrics with:', statistics);
        if (!statistics) {
            console.warn('No statistics data provided');
            return;
        }
        
        // Store statistics for pagination total count calculations
        if (!this.dashboardStats) {
            this.dashboardStats = {};
        }
        this.dashboardStats.totalLogs = statistics.totalLogs;

        const totalLogsEl = document.getElementById('totalLogs');
        const errorCountEl = document.getElementById('errorCount');
        const avgResponseTimeEl = document.getElementById('avgResponseTime');
        const securityEventsEl = document.getElementById('securityEvents');
        const totalAuditLogsEl = document.getElementById('totalAuditLogs');
        const todayAuditLogsEl = document.getElementById('todayAuditLogs');
        const failedOperationsEl = document.getElementById('failedOperations');

        if (totalLogsEl) totalLogsEl.textContent = this.formatNumber(statistics.totalLogs || 0);
        if (errorCountEl) errorCountEl.textContent = this.formatNumber(statistics.errorCount || 0);
        if (avgResponseTimeEl) avgResponseTimeEl.textContent = `${Math.round(statistics.avgResponseTime || 0)}ms`;
        if (securityEventsEl) securityEventsEl.textContent = this.formatNumber(statistics.securityEvents || 0);
        if (totalAuditLogsEl) totalAuditLogsEl.textContent = this.formatNumber(statistics.totalAuditLogs || 0);
        if (todayAuditLogsEl) todayAuditLogsEl.textContent = this.formatNumber(statistics.todayAuditLogs || 0);
        if (failedOperationsEl) failedOperationsEl.textContent = this.formatNumber(statistics.failedOperations || 0);
        
        console.log('Metrics updated successfully');
    }

    updateAuditMetrics(auditStatistics) {
        if (!auditStatistics) return;
        
        // Store audit statistics for pagination total count calculations
        if (!this.dashboardStats) {
            this.dashboardStats = {};
        }
        this.dashboardStats.auditStatistics = auditStatistics;
        
        document.getElementById('avgAuditDuration').textContent = `${Math.round(auditStatistics.avgExecutionDuration)}ms`;
    }

    updateCharts(data) {
        console.log('Updating charts with data:', data);
        
        // Skip chart updates if Chart.js is not loaded to prevent blocking other UI updates
        if (typeof window.Chart === 'undefined') {
            console.warn('Chart.js not loaded, skipping chart updates');
            return;
        }
        
        try {
            if (data.logLevelCounts) {
                this.updateLogLevelChart(data.logLevelCounts);
            } else {
                console.warn('No logLevelCounts data');
            }
        } catch (error) {
            console.error('Error updating log level chart:', error);
        }
        
        try {
            if (data.applicationCounts) {
                this.updateApplicationChart(data.applicationCounts);
            } else {
                console.warn('No applicationCounts data');
            }
        } catch (error) {
            console.error('Error updating application chart:', error);
        }
        
        try {
            if (data.hourlyCounts) {
                this.updateHourlyTrendsChart(data.hourlyCounts);
            } else {
                console.warn('No hourlyCounts data');
            }
        } catch (error) {
            console.error('Error updating hourly trends chart:', error);
        }
        
        console.log('Charts update attempted');
    }

    updateLogLevelChart(logLevelCounts) {
        console.log('updateLogLevelChart called with:', logLevelCounts);
        
        try {
            const canvas = document.getElementById('logLevelChart');
            if (!canvas) {
                console.error('logLevelChart canvas element not found!');
                return;
            }

            const ctx = canvas.getContext('2d');
            if (!ctx) {
                console.error('Could not get 2D context for logLevelChart');
                return;
            }

            if (!window.Chart) {
                console.error('Chart.js library not loaded!');
                return;
            }
            
            if (this.charts.logLevel) {
                this.charts.logLevel.destroy();
            }

            this.charts.logLevel = new Chart(ctx, {
                type: 'doughnut',
                data: {
                    labels: logLevelCounts.map(item => item.level),
                    datasets: [{
                        data: logLevelCounts.map(item => item.count),
                        backgroundColor: [
                            '#28a745', // Information - Green
                            '#ffc107', // Warning - Yellow
                            '#dc3545', // Error - Red
                            '#6f42c1'  // Critical - Purple
                        ],
                        borderWidth: 2,
                        borderColor: '#fff'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            position: 'bottom'
                        }
                    }
                }
            });
            
            console.log('Log level chart updated successfully');
        } catch (error) {
            console.error('Error updating log level chart:', error);
        }
    }

    updateApplicationChart(applicationCounts) {
        try {
            const canvas = document.getElementById('applicationChart');
            if (!canvas) {
                console.error('applicationChart canvas element not found!');
                return;
            }

            const ctx = canvas.getContext('2d');
            if (!ctx) {
                console.error('Could not get 2D context for applicationChart');
                return;
            }
            
            if (this.charts.application) {
                this.charts.application.destroy();
            }

            this.charts.application = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: applicationCounts.map(item => item.application.replace('ERPPlatform.', '')),
                datasets: [
                    {
                        label: 'Total Logs',
                        data: applicationCounts.map(item => item.count),
                        backgroundColor: '#007bff',
                        borderColor: '#0056b3',
                        borderWidth: 1
                    },
                    {
                        label: 'Errors',
                        data: applicationCounts.map(item => item.errorCount),
                        backgroundColor: '#dc3545',
                        borderColor: '#bd2130',
                        borderWidth: 1
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    y: {
                        beginAtZero: true
                    }
                },
                plugins: {
                    legend: {
                        position: 'top'
                    }
                }
            }
        });
        
        console.log('Application chart updated successfully');
        } catch (error) {
            console.error('Error updating application chart:', error);
        }
    }

    updateHourlyTrendsChart(hourlyCounts) {
        try {
            const canvas = document.getElementById('hourlyTrendsChart');
            if (!canvas) {
                console.error('hourlyTrendsChart canvas element not found!');
                return;
            }

            const ctx = canvas.getContext('2d');
            if (!ctx) {
                console.error('Could not get 2D context for hourlyTrendsChart');
                return;
            }
            
            if (this.charts.hourlyTrends) {
                this.charts.hourlyTrends.destroy();
            }

            this.charts.hourlyTrends = new Chart(ctx, {
            type: 'line',
            data: {
                labels: hourlyCounts.map(item => this.formatHour(item.hour)),
                datasets: [
                    {
                        label: 'Total',
                        data: hourlyCounts.map(item => item.totalCount),
                        borderColor: '#007bff',
                        backgroundColor: 'rgba(0, 123, 255, 0.1)',
                        tension: 0.4
                    },
                    {
                        label: 'Errors',
                        data: hourlyCounts.map(item => item.errorCount),
                        borderColor: '#dc3545',
                        backgroundColor: 'rgba(220, 53, 69, 0.1)',
                        tension: 0.4
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    y: {
                        beginAtZero: true
                    }
                },
                plugins: {
                    legend: {
                        position: 'top'
                    }
                }
            }
        });
        
        console.log('Hourly trends chart updated successfully');
        } catch (error) {
            console.error('Error updating hourly trends chart:', error);
        }
    }

    updateRecentLogs(recentLogs, isSearchResult = false) {
        console.log('updateRecentLogs called with:', recentLogs, 'isSearchResult:', isSearchResult);
        
        if (isSearchResult) {
            // For search results, just display the data without calling server pagination
            console.log('Displaying search results');
            this.paginationData.recentLogs.data = recentLogs || [];
            this.paginationData.recentLogs.currentSkip = 0;
            this.paginationData.recentLogs.totalCount = recentLogs ? recentLogs.length : 0;
            this.paginationData.recentLogs.hasMore = false; // Search results are usually complete
            this.renderRecentLogsContent();
            this.updateRecentLogsPagination();
        } else if (recentLogs && recentLogs.length > 0) {
            // Regular dashboard load - show data immediately then get correct totals
            console.log('Using provided dashboard data for immediate display, then loading server pagination for correct totals');
            this.paginationData.recentLogs.data = recentLogs;
            this.renderRecentLogsContent();
            
            // Immediately load server-side pagination to get correct totals (no delay)
            this.loadRecentLogsPaginated(0, this.paginationData.recentLogs.take);
        } else {
            // No initial data, load directly from server
            console.log('No initial data provided, loading from server');
            this.loadRecentLogsPaginated(0, this.paginationData.recentLogs.take);
        }
    }

    updateRecentLogsFromSearch(result) {
        console.log('updateRecentLogsFromSearch called with:', result);
        
        // Update pagination data with search results
        // Note: API returns 'page' not 'currentPage'
        const currentPage = result.page || result.currentPage || 1;
        this.paginationData.recentLogs.data = result.items || [];
        this.paginationData.recentLogs.currentSkip = (currentPage - 1) * result.pageSize;
        this.paginationData.recentLogs.totalCount = result.totalCount || 0;
        this.paginationData.recentLogs.hasMore = currentPage < result.totalPages;
        this.paginationData.recentLogs.take = result.pageSize || 10;
        
        // Render content and update pagination display
        this.renderRecentLogsContent();
        this.updateRecentLogsPagination();
    }
    
    async searchRecentLogs(pageNumber = 1) {
        if (!this.recentLogsSearchContext || !this.recentLogsSearchContext.isActive) {
            console.log('No active search context');
            return;
        }
        
        try {
            // Update page number in search request
            const searchRequest = { ...this.recentLogsSearchContext.searchRequest };
            searchRequest.page = pageNumber;
            
            // Get antiforgery token
            const token = this.getAntiforgeryToken();
            const headers = {
                'Content-Type': 'application/json'
            };
            
            if (token) {
                headers['RequestVerificationToken'] = token;
            }
            
            const response = await fetch('/api/log-analytics/search', {
                method: 'POST',
                headers: headers,
                body: JSON.stringify(searchRequest)
            });

            const result = await response.json();
            this.updateRecentLogsFromSearch(result);
            
        } catch (error) {
            console.error('Search pagination failed:', error);
        }
    }
    
    clearRecentLogsSearch() {
        console.log('Clearing recent logs search context');
        this.recentLogsSearchContext = {
            searchRequest: null,
            isActive: false
        };
        
        // Reload regular recent logs
        this.loadRecentLogsPaginated(0, this.paginationData.recentLogs.take);
    }

    async loadRecentLogsPaginated(skip, take) {
        try {
            console.log(`Loading recent logs with skip=${skip}, take=${take}`);
            const response = await fetch(`/api/log-analytics/recent-logs/paginated?skip=${skip}&take=${take}`);
            const result = await response.json();
            
            console.log('Server-side pagination result:', result);
            
            // Update pagination data
            this.paginationData.recentLogs.data = result.items || [];
            this.paginationData.recentLogs.currentSkip = skip;
            this.paginationData.recentLogs.totalCount = result.totalCount || 0;
            this.paginationData.recentLogs.hasMore = result.hasMore || false;
            
            this.renderRecentLogsContent();
            this.updateRecentLogsPagination();
        } catch (error) {
            console.error('Error loading recent logs:', error);
        }
    }

    async loadRecentAuditLogsPaginated(skip, take) {
        try {
            console.log(`Loading recent audit logs with skip=${skip}, take=${take}`);
            const response = await fetch(`/api/log-analytics/audit-logs/recent/paginated?skip=${skip}&take=${take}`);
            const result = await response.json();
            
            console.log('Server-side audit pagination result:', result);
            
            // Update pagination data
            this.paginationData.recentAuditLogs.data = result.items || [];
            this.paginationData.recentAuditLogs.currentSkip = skip;
            this.paginationData.recentAuditLogs.totalCount = result.totalCount || 0;
            this.paginationData.recentAuditLogs.hasMore = result.hasMore || false;
            
            this.renderRecentAuditLogsContent();
            this.updateRecentAuditLogsPagination();
        } catch (error) {
            console.error('Error loading recent audit logs:', error);
        }
    }

    renderRecentLogsContent() {
        const container = document.getElementById('recentLogsContainer');
        if (!container) return;
        
        const data = this.paginationData.recentLogs.data;
        if (!data || data.length === 0) {
            container.innerHTML = '<div class="text-center text-muted p-4">No recent logs found</div>';
            return;
        }
        
        const html = this.renderRecentLogsHtml(data);
        container.innerHTML = html;
    }

    renderRecentAuditLogsContent() {
        const container = document.getElementById('recentAuditLogsContainer');
        if (!container) return;
        
        const data = this.paginationData.recentAuditLogs.data;
        if (!data || data.length === 0) {
            container.innerHTML = '<div class="text-center text-muted p-4">No recent audit logs found</div>';
            return;
        }
        
        const html = this.renderRecentAuditLogsHtml(data);
        container.innerHTML = html;
    }

    updateRecentLogsPagination() {
        const pagination = document.getElementById('recentLogsPagination');
        if (!pagination) return;
        
        const pagData = this.paginationData.recentLogs;
        const currentPage = Math.floor(pagData.currentSkip / pagData.take) + 1;
        
        // Always show pagination if we have data and potential for more pages
        if (pagData.data && pagData.data.length > 0) {
            pagination.style.display = 'flex';
            const prevBtn = pagination.querySelector('.btn:first-child');
            const nextBtn = pagination.querySelector('.btn:last-child');
            const info = document.getElementById('recentLogsInfo'); // Use correct ID
            
            prevBtn.disabled = pagData.currentSkip === 0;
            nextBtn.disabled = !pagData.hasMore;
            
            if (info) {
                if (pagData.totalCount > 0) {
                    const startItem = pagData.currentSkip + 1;
                    const endItem = Math.min(pagData.currentSkip + pagData.take, pagData.totalCount);
                    info.textContent = `Showing ${startItem}-${endItem} of ${pagData.totalCount} items`;
                } else {
                    // Initial state - show basic info with current data
                    const startItem = pagData.currentSkip + 1;
                    const endItem = pagData.currentSkip + pagData.data.length;
                    info.textContent = `Showing ${startItem}-${endItem} of ${pagData.data.length}+ items`;
                }
            }
        } else {
            pagination.style.display = 'none';
        }
    }

    updateRecentAuditLogsPagination() {
        const pagination = document.getElementById('recentAuditLogsPagination');
        if (!pagination) return;
        
        const pagData = this.paginationData.recentAuditLogs;
        const currentPage = Math.floor(pagData.currentSkip / pagData.take) + 1;
        
        // Always show pagination if we have data and potential for more pages
        if (pagData.data && pagData.data.length > 0) {
            pagination.style.display = 'flex';
            const prevBtn = pagination.querySelector('.btn:first-child');
            const nextBtn = pagination.querySelector('.btn:last-child');
            const info = document.getElementById('recentAuditLogsInfo'); // Use correct ID
            
            prevBtn.disabled = pagData.currentSkip === 0;
            nextBtn.disabled = !pagData.hasMore;
            
            if (info) {
                if (pagData.totalCount > 0) {
                    const startItem = pagData.currentSkip + 1;
                    const endItem = Math.min(pagData.currentSkip + pagData.take, pagData.totalCount);
                    info.textContent = `Showing ${startItem}-${endItem} of ${pagData.totalCount} activities`;
                } else {
                    // Initial state - show basic info with current data
                    const startItem = pagData.currentSkip + 1;
                    const endItem = pagData.currentSkip + pagData.data.length;
                    info.textContent = `Showing ${startItem}-${endItem} of ${pagData.data.length}+ activities`;
                }
            }
        } else {
            pagination.style.display = 'none';
        }
    }

    updateTopErrors(topErrors) {
        console.log('updateTopErrors called with:', topErrors);
        
        const container = document.getElementById('topErrorsContainer');
        if (!container) {
            console.error('topErrorsContainer not found!');
            return;
        }

        if (!topErrors || topErrors.length === 0) {
            container.innerHTML = '<div class="text-center p-3 text-muted">No errors found</div>';
            return;
        }

        const html = this.renderTopErrorsHtml(topErrors);
        container.innerHTML = html;
        console.log('Top Errors container updated successfully');
        
        // Store data for pagination (if needed later)
        this.paginationData.topErrors.data = topErrors || [];
        this.paginationData.topErrors.currentPage = 1;
    }

    updatePerformanceMetrics(performanceMetrics) {
        console.log('updatePerformanceMetrics called with:', performanceMetrics);
        
        const container = document.getElementById('performanceContainer');
        if (!container) {
            console.error('performanceContainer not found!');
            return;
        }

        if (!performanceMetrics || performanceMetrics.length === 0) {
            container.innerHTML = '<div class="text-center p-3 text-muted">No performance data available</div>';
            return;
        }

        // Show top 5 metrics to fit in the container
        const topMetrics = performanceMetrics.slice(0, 5);
        const html = this.renderPerformanceHtml(topMetrics);
        container.innerHTML = html;
        console.log('Performance Metrics container updated successfully');
        
        // Store data for pagination (if needed later)
        this.paginationData.performance.data = performanceMetrics || [];
        this.paginationData.performance.currentPage = 1;
    }

    async checkSystemHealth() {
        try {
            const response = await fetch('/api/log-analytics/system-health');
            const health = await response.json();
            
            const alert = document.getElementById('systemHealthAlert');
            const title = document.getElementById('healthAlertTitle');
            const message = document.getElementById('healthAlertMessage');
            
            let alertClass = 'alert-success';
            let statusText = 'System Healthy';
            
            if (health.status === 'Critical') {
                alertClass = 'alert-danger';
                statusText = 'System Critical';
            } else if (health.status === 'Warning') {
                alertClass = 'alert-warning';
                statusText = 'System Warning';
            }
            
            alert.className = `alert ${alertClass} alert-dismissible fade show`;
            title.textContent = statusText;
            message.textContent = `${health.recentErrors} errors, ${health.recentCritical} critical events in the last hour. Avg response: ${health.avgResponseTime}ms`;
            
            if (health.status !== 'Healthy') {
                alert.classList.remove('d-none');
            }
        } catch (error) {
            console.error('Failed to check system health:', error);
        }
    }

    toggleAutoRefresh() {
        if (this.isAutoRefreshing) {
            clearInterval(this.autoRefreshInterval);
            this.isAutoRefreshing = false;
            document.getElementById('autoRefreshIcon').className = 'fas fa-play';
        } else {
            this.autoRefreshInterval = setInterval(() => {
                this.loadDashboardData();
            }, this.refreshIntervalMs);
            this.isAutoRefreshing = true;
            document.getElementById('autoRefreshIcon').className = 'fas fa-pause';
        }
    }

    showLogSearch() {
        // Set maximum date to current datetime to prevent future dates
        const now = new Date();
        const maxDateTime = new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
        
        document.getElementById('fromDate').setAttribute('max', maxDateTime);
        document.getElementById('toDate').setAttribute('max', maxDateTime);
        
        // Load applications data when modal is shown
        this.loadApplications();
        
        this.safeShowModal('logSearchModal');
    }

    async searchLogs() {
        console.log('=== SEARCH LOGS CALLED ===');
        
        // Prevent concurrent searches
        if (this.logSearchInProgress) {
            console.log('Log search already in progress, ignoring request');
            return;
        }
        
        // Validate date range first
        if (!this.validateDateRange()) {
            console.log('Date validation failed, cannot proceed with search');
            return;
        }
        
        this.logSearchInProgress = true;
        
        const form = document.getElementById('logSearchForm');
        const formData = new FormData(form);
        
        const searchRequest = {
            fromDate: formData.get('fromDate') || null,
            toDate: formData.get('toDate') || null,
            logLevels: formData.getAll('logLevels'),
            applications: formData.getAll('applications'),
            searchText: formData.get('searchText') || null,
            userId: formData.get('userId') || null,
            category: formData.get('category') || null,
            page: 1,
            pageSize: 10
        };

        try {
            // Get antiforgery token
            const token = this.getAntiforgeryToken();
            const headers = {
                'Content-Type': 'application/json'
            };
            
            if (token) {
                headers['RequestVerificationToken'] = token;
            }

            console.log('Sending search request:', searchRequest);
            console.log('Request headers:', headers);
            
            const response = await fetch('/api/log-analytics/search', {
                method: 'POST',
                headers: headers,
                body: JSON.stringify(searchRequest)
            });

            console.log('Search response status:', response.status);
            
            if (!response.ok) {
                const errorText = await response.text();
                console.error('Search API error:', response.status, errorText);
                throw new Error(`Search failed: ${response.status} - ${errorText}`);
            }

            const result = await response.json();
            console.log('Search result:', result);
            
            // Store search context for pagination
            this.recentLogsSearchContext = {
                searchRequest: searchRequest,
                isActive: true
            };
            
            this.updateRecentLogsFromSearch(result);
            
            // Close modal
            this.safeHideModal('logSearchModal');
            
        } catch (error) {
            console.error('Search failed:', error);
            this.showError('Search failed');
        } finally {
            this.logSearchInProgress = false;
        }
    }

    showExportDialog() {
        // Simple export implementation
        const fromDate = prompt('Export from date (YYYY-MM-DD):');
        const toDate = prompt('Export to date (YYYY-MM-DD):');
        
        if (fromDate && toDate) {
            // Ensure fromDate starts at beginning of day and toDate ends at end of day
            const fromDateTime = fromDate.includes('T') ? fromDate : fromDate + 'T00:00:00';
            const toDateTime = toDate.includes('T') ? toDate : toDate + 'T23:59:59';
            
            const exportRequest = {
                fromDate: fromDateTime,
                toDate: toDateTime,
                logLevels: [],
                applications: [],
                page: 1,
                pageSize: 10000
            };

            const queryString = new URLSearchParams({
                format: 'csv'
            }).toString();

            // Get antiforgery token
            const token = this.getAntiforgeryToken();
            const headers = {
                'Content-Type': 'application/json'
            };
            
            if (token) {
                headers['RequestVerificationToken'] = token;
            }

            fetch(`/api/log-analytics/export?${queryString}`, {
                method: 'POST',
                headers: headers,
                body: JSON.stringify(exportRequest)
            })
            .then(response => {
                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }
                return response.blob();
            })
            .then(blob => {
                const url = window.URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = `logs_export_${new Date().toISOString().split('T')[0]}.csv`;
                a.click();
                window.URL.revokeObjectURL(url);
            })
            .catch(error => {
                console.error('Export failed:', error);
                this.showError('Export failed: ' + error.message);
            });
        }
    }

    getAntiforgeryToken() {
        // Try to get the antiforgery token from various possible sources
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        if (tokenInput) {
            console.log('Found antiforgery token in input:', tokenInput.value);
            return tokenInput.value;
        }
        
        const tokenMeta = document.querySelector('meta[name="__RequestVerificationToken"]');
        if (tokenMeta) {
            console.log('Found antiforgery token in meta:', tokenMeta.content);
            return tokenMeta.content;
        }
        
        // Try to get from ABP's token container
        const abpToken = document.querySelector('input[name="RequestVerificationToken"]');
        if (abpToken) {
            console.log('Found ABP antiforgery token:', abpToken.value);
            return abpToken.value;
        }
        
        // Try to get from cookie
        const tokenCookie = document.cookie
            .split('; ')
            .find(row => row.startsWith('__RequestVerificationToken=') || row.startsWith('.AspNetCore.Antiforgery.'));
        if (tokenCookie) {
            const tokenValue = tokenCookie.split('=')[1];
            console.log('Found antiforgery token in cookie:', tokenValue);
            return tokenValue;
        }
        
        console.warn('No antiforgery token found');
        return null;
    }

    // Utility methods
    formatNumber(num) {
        return new Intl.NumberFormat().format(num);
    }

    formatDateTime(dateStr) {
        const date = new Date(dateStr);
        return date.toLocaleString();
    }

    formatHour(dateStr) {
        const date = new Date(dateStr);
        return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    }

    escapeHtml(unsafe) {
        return unsafe
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    getPerformanceClass(duration) {
        if (duration > 5000) return 'slow';
        if (duration > 1000) return 'normal';
        return 'fast';
    }

    updateRecentAuditLogs(auditLogs) {
        console.log('updateRecentAuditLogs called with:', auditLogs);
        
        // Always load from server to get correct totals, but show initial data immediately for better UX
        if (auditLogs && auditLogs.length > 0) {
            console.log('Using provided dashboard audit data for immediate display, then loading server pagination for correct totals');
            this.paginationData.recentAuditLogs.data = auditLogs;
            this.renderRecentAuditLogsContent();
            
            // Immediately load server-side pagination to get correct totals (no delay)
            this.loadRecentAuditLogsPaginated(0, this.paginationData.recentAuditLogs.take);
        } else {
            // No initial audit data, load directly from server
            console.log('No initial audit data provided, loading from server');
            this.loadRecentAuditLogsPaginated(0, this.paginationData.recentAuditLogs.take);
        }
    }

    updateTopUserActivities(userActivities) {
        console.log('updateTopUserActivities called with:', userActivities);
        
        const container = document.getElementById('topUserActivitiesContainer');
        if (!container) {
            console.error('topUserActivitiesContainer not found!');
            return;
        }

        if (!userActivities || userActivities.length === 0) {
            container.innerHTML = '<div class="text-center p-3 text-muted">No user activity data available</div>';
            return;
        }

        // Show top 10 users
        const topUsers = userActivities.slice(0, 10);
        const html = this.renderTopUserActivitiesHtml(topUsers);
        container.innerHTML = html;
        console.log('Top User Activities container updated successfully');
        
        // Store data for pagination (if needed later)
        this.paginationData.topUserActivities.data = userActivities || [];
        this.paginationData.topUserActivities.currentPage = 1;
    }

    // Helper method to safely hide modals without accessibility conflicts
    safeHideModal(modalId) {
        const modal = bootstrap.Modal.getInstance(document.getElementById(modalId));
        const modalElement = document.getElementById(modalId);
        
        if (modal && modalElement) {
            // Remove focus from any buttons inside the modal before hiding
            const focusedElement = modalElement.querySelector(':focus');
            if (focusedElement) {
                focusedElement.blur();
            }
            
            // Remove aria-hidden to prevent conflicts during hiding
            modalElement.removeAttribute('aria-hidden');
            
            modal.hide();
        }
    }
    
    // Helper method to safely show modals with proper accessibility
    safeShowModal(modalId) {
        const modalElement = document.getElementById(modalId);
        if (modalElement) {
            // Clear any existing aria-hidden attributes
            modalElement.removeAttribute('aria-hidden');
            
            const modal = new bootstrap.Modal(modalElement);
            modal.show();
        }
    }

    showAuditLogSearch() {
        // Get today's date in YYYY-MM-DD format (local timezone)
        const today = new Date();
        const maxDate = today.getFullYear() + '-' + 
                       String(today.getMonth() + 1).padStart(2, '0') + '-' + 
                       String(today.getDate()).padStart(2, '0');
        
        // Set maximum date to prevent future dates in calendar popup
        const fromDateInput = document.getElementById('auditFromDate');
        const toDateInput = document.getElementById('auditToDate');
        
        if (fromDateInput) {
            fromDateInput.setAttribute('max', maxDate);
            fromDateInput.max = maxDate;
            console.log('From date max set to:', maxDate, 'Attribute:', fromDateInput.getAttribute('max'));
        }
        
        if (toDateInput) {
            toDateInput.setAttribute('max', maxDate);
            toDateInput.max = maxDate;
            console.log('To date max set to:', maxDate, 'Attribute:', toDateInput.getAttribute('max'));
        }
        
        console.log('Audit date inputs max constraints applied:', maxDate);
        
        // Show the audit log search modal
        this.safeShowModal('auditLogSearchModal');
        
        // Re-apply constraints after modal is shown (for browser compatibility)
        setTimeout(() => {
            const fromInput = document.getElementById('auditFromDate');
            const toInput = document.getElementById('auditToDate');
            if (fromInput) {
                fromInput.setAttribute('max', maxDate);
                fromInput.max = maxDate;
            }
            if (toInput) {
                toInput.setAttribute('max', maxDate);
                toInput.max = maxDate;
            }
            console.log('Date constraints re-applied after modal shown');
        }, 100);
    }

    async searchAuditLogs(pageNumber = 1) {
        console.log('=== SEARCH AUDIT LOGS CALLED ===');
        console.log('Page Number:', pageNumber);
        console.log('Current pageSize:', this.auditSearchState.pageSize);
        console.log('Call stack:', new Error().stack);
        
        // Prevent concurrent searches
        if (this.auditSearchInProgress) {
            console.log('Audit search already in progress, ignoring request');
            return;
        }
        
        this.auditSearchInProgress = true;
        this.disableAuditPaginationButtons();
        
        try {
            // Validate date range first (only on first search, not pagination, and only if dates are provided)
            if (pageNumber === 1) {
                const fromDate = document.getElementById('auditFromDate').value;
                const toDate = document.getElementById('auditToDate').value;
                
                // Only validate if at least one date is provided
                if ((fromDate || toDate) && !this.validateAuditDateRange()) {
                    console.log('Audit date validation failed, cannot proceed with search');
                    return;
                }
            }
            
            let searchRequest;
            
            if (pageNumber === 1) {
                // First search - get form values
                const fromDate = document.getElementById('auditFromDate').value;
                const toDate = document.getElementById('auditToDate').value;
                const userId = document.getElementById('auditUserId').value;
                const serviceName = document.getElementById('auditServiceName').value;
                const methodName = document.getElementById('auditMethodName').value;
                const httpMethod = document.getElementById('auditHttpMethod').value;
                const clientIp = document.getElementById('auditClientIp').value;
                const hasException = document.getElementById('auditHasException').value;
                const minDuration = document.getElementById('auditMinDuration').value;
                const maxDuration = document.getElementById('auditMaxDuration').value;

                // Build search request
                searchRequest = {
                    page: pageNumber,
                    pageSize: this.auditSearchState.pageSize
                };

                // Add date filters with proper time formatting (only if provided)
                if (fromDate && fromDate.trim() !== '') {
                    searchRequest.fromDate = fromDate + 'T00:00:00';
                }
                if (toDate && toDate.trim() !== '') {
                    searchRequest.toDate = toDate + 'T23:59:59';
                }

                // Add other filters if provided
                if (userId) searchRequest.userId = userId;
                if (serviceName) searchRequest.serviceName = serviceName;
                if (methodName) searchRequest.methodName = methodName;
                if (httpMethod) searchRequest.httpMethod = httpMethod;
                if (clientIp) searchRequest.clientIp = clientIp;
                if (hasException !== '') searchRequest.hasException = hasException === 'true';
                if (minDuration) searchRequest.minDuration = parseInt(minDuration);
                if (maxDuration) searchRequest.maxDuration = parseInt(maxDuration);
                
                // Store the search request for pagination
                this.auditSearchState.searchRequest = searchRequest;
            } else {
                // Pagination - use stored search request with new page number
                searchRequest = { ...this.auditSearchState.searchRequest };
                searchRequest.page = pageNumber;
            }

            this.auditSearchState.currentPage = pageNumber;

            // Get antiforgery token
            const token = this.getAntiforgeryToken();
            const headers = {
                'Content-Type': 'application/json'
            };
            
            if (token) {
                headers['RequestVerificationToken'] = token;
            }

            // Make API call
            const response = await fetch('/api/log-analytics/audit-logs/search', {
                method: 'POST',
                headers: headers,
                body: JSON.stringify(searchRequest)
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const result = await response.json();
            
            console.log('=== AUDIT SEARCH DEBUG ===');
            console.log('Page:', pageNumber, 'PageSize:', this.auditSearchState.pageSize);
            console.log('AuditLogs count received:', result.items ? result.items.length : 0);
            console.log('TotalCount:', result.totalCount, 'TotalPages:', result.totalPages);
            console.log('Response result:', result);
            
            // Store pagination information
            console.log('=== STORING PAGINATION STATE ===');
            console.log('API result.totalCount:', result.totalCount);
            console.log('API result.totalPages:', result.totalPages);
            console.log('API result.page:', result.page);
            console.log('API result.pageSize:', result.pageSize);
            
            this.auditSearchState.totalCount = result.totalCount;
            this.auditSearchState.totalPages = result.totalPages;
            
            console.log('Stored auditSearchState.totalCount:', this.auditSearchState.totalCount);
            console.log('Stored auditSearchState.totalPages:', this.auditSearchState.totalPages);
            
            // Display results
            this.displayAuditLogSearchResults(result);
            
            // Update pagination info
            this.updateAuditSearchPagination();
            
            if (pageNumber === 1) {
                // Hide search modal and show results modal only on first search
                const searchModal = bootstrap.Modal.getInstance(document.getElementById('auditLogSearchModal'));
                const resultsModalElement = document.getElementById('auditLogSearchResultsModal');
                
                // Hide search modal first
                this.safeHideModal('auditLogSearchModal');
                
                // Wait for search modal to fully hide before showing results modal
                const self = this;
                searchModal._element.addEventListener('hidden.bs.modal', function showResultsModal() {
                    // Remove event listener to prevent multiple calls
                    searchModal._element.removeEventListener('hidden.bs.modal', showResultsModal);
                    
                    // Show results modal with safe method
                    self.safeShowModal('auditLogSearchResultsModal');
                }, { once: true });
            }

        } catch (error) {
            console.error('Error searching audit logs:', error);
            alert('Error searching audit logs: ' + error.message);
        } finally {
            this.auditSearchInProgress = false;
            this.enableAuditPaginationButtons();
        }
    }

    displayAuditLogSearchResults(result) {
        const container = document.getElementById('auditLogSearchResults');
        
        console.log('=== DISPLAY AUDIT RESULTS DEBUG ===');
        console.log('Container element found:', !!container);
        console.log('Container current children count:', container ? container.children.length : 'N/A');
        console.log('Result object:', result);
        console.log('result.items array:', result.items);
        console.log('Results to display:', result.items ? result.items.length : 0);
        
        if (!result.items || result.items.length === 0) {
            container.innerHTML = '<div class="alert alert-info">No audit logs found matching your search criteria.</div>';
            return;
        }
        
        // Clear any existing content to prevent accumulation
        container.innerHTML = '';

        let html = `
            <div class="table-responsive">
                <table class="table table-striped table-hover">
                    <thead class="table-dark">
                        <tr>
                            <th>Time</th>
                            <th>User</th>
                            <th>Service</th>
                            <th>Method</th>
                            <th>Duration</th>
                            <th>Status</th>
                            <th>IP</th>
                        </tr>
                    </thead>
                    <tbody>
        `;

        result.items.forEach(log => {
            const statusClass = log.hasException ? 'text-danger' : 'text-success';
            const statusIcon = log.hasException ? 'fas fa-times' : 'fas fa-check';
            
            html += `
                <tr>
                    <td><small>${new Date(log.executionTime).toLocaleString()}</small></td>
                    <td>
                        <div>${log.userName || 'Anonymous'}</div>
                        <small class="text-muted">${log.userId || ''}</small>
                    </td>
                    <td><small>${log.serviceName}</small></td>
                    <td>
                        <div>${log.methodName}</div>
                        <small class="text-muted">${log.httpMethod} ${log.httpStatusCode || ''}</small>
                    </td>
                    <td><span class="badge ${log.executionDuration > 1000 ? 'bg-warning' : 'bg-light text-dark'}">${log.executionDuration}ms</span></td>
                    <td><i class="${statusIcon} ${statusClass}"></i></td>
                    <td><small>${log.clientIpAddress || ''}</small></td>
                </tr>
            `;
        });

        html += `
                    </tbody>
                </table>
            </div>
        `;

        container.innerHTML = html;
        
        // Debug: Count the actual rows rendered in the DOM
        const tableRows = container.querySelectorAll('tbody tr');
        console.log('=== FINAL DOM DEBUG ===');
        console.log('Table rows rendered in DOM:', tableRows.length);
        console.log('Expected rows:', result.auditLogs ? result.auditLogs.length : 0);
        console.log('Container final innerHTML length:', html.length);
    }

    updateAuditSearchPagination() {
        const infoElement = document.getElementById('auditSearchResultsInfo');
        const paginationElement = document.getElementById('auditSearchPagination');
        const prevBtn = document.getElementById('auditSearchPrevBtn');
        const nextBtn = document.getElementById('auditSearchNextBtn');
        
        if (this.auditSearchState.totalCount === 0) {
            infoElement.textContent = 'No results found';
            paginationElement.style.display = 'none';
            return;
        }
        
        // Calculate display info
        const start = (this.auditSearchState.currentPage - 1) * this.auditSearchState.pageSize + 1;
        const end = Math.min(this.auditSearchState.currentPage * this.auditSearchState.pageSize, this.auditSearchState.totalCount);
        
        console.log('=== PAGINATION INFO DEBUG ===');
        console.log('Current Page:', this.auditSearchState.currentPage);
        console.log('Page Size:', this.auditSearchState.pageSize);
        console.log('Total Count:', this.auditSearchState.totalCount);
        console.log('Total Pages:', this.auditSearchState.totalPages);
        console.log('Calculated start:', start);
        console.log('Calculated end:', end);
        
        infoElement.textContent = `Showing ${start}-${end} of ${this.auditSearchState.totalCount} results`;
        
        // Show pagination controls if needed
        if (this.auditSearchState.totalPages > 1) {
            paginationElement.style.display = 'block';
            
            // Update button states
            prevBtn.disabled = this.auditSearchState.currentPage === 1;
            nextBtn.disabled = this.auditSearchState.currentPage === this.auditSearchState.totalPages;
        } else {
            paginationElement.style.display = 'none';
        }
    }

    async previousAuditSearchPage() {
        console.log('=== PREVIOUS AUDIT SEARCH PAGE CALLED ===');
        console.log('Current page:', this.auditSearchState.currentPage);
        console.log('Search in progress:', this.auditSearchInProgress);
        
        if (this.auditSearchInProgress) {
            console.log('Search already in progress, ignoring previous page request');
            return;
        }
        
        if (this.auditSearchState.currentPage > 1) {
            console.log('Calling searchAuditLogs for page:', this.auditSearchState.currentPage - 1);
            await this.searchAuditLogs(this.auditSearchState.currentPage - 1);
        } else {
            console.log('Already at first page, cannot go to previous');
        }
    }

    async nextAuditSearchPage() {
        console.log('=== NEXT AUDIT SEARCH PAGE CALLED ===');
        console.log('Current page:', this.auditSearchState.currentPage);
        console.log('Total pages:', this.auditSearchState.totalPages);
        console.log('Search in progress:', this.auditSearchInProgress);
        
        if (this.auditSearchInProgress) {
            console.log('Search already in progress, ignoring next page request');
            return;
        }
        
        if (this.auditSearchState.currentPage < this.auditSearchState.totalPages) {
            console.log('Calling searchAuditLogs for page:', this.auditSearchState.currentPage + 1);
            await this.searchAuditLogs(this.auditSearchState.currentPage + 1);
        } else {
            console.log('Already at last page, cannot go to next');
        }
    }

    disableAuditPaginationButtons() {
        const prevBtn = document.getElementById('auditSearchPrevBtn');
        const nextBtn = document.getElementById('auditSearchNextBtn');
        if (prevBtn) prevBtn.disabled = true;
        if (nextBtn) nextBtn.disabled = true;
    }

    enableAuditPaginationButtons() {
        const prevBtn = document.getElementById('auditSearchPrevBtn');
        const nextBtn = document.getElementById('auditSearchNextBtn');
        if (prevBtn) prevBtn.disabled = this.auditSearchState.currentPage === 1;
        if (nextBtn) nextBtn.disabled = this.auditSearchState.currentPage === this.auditSearchState.totalPages;
    }

    // Log Search form control functions
    resetLogSearchForm() {
        // Reset all form fields
        document.getElementById('logSearchForm').reset();
        
        // Clear validation errors
        this.clearDateValidationErrors();
        
        // Clear search results and restore normal pagination
        console.log('Log search form reset to default values, restoring normal pagination');
        this.loadRecentLogsPaginated(0, this.paginationData.recentLogs.take);
    }

    validateDateRange() {
        const fromDateInput = document.getElementById('fromDate');
        const toDateInput = document.getElementById('toDate');
        const fromDateError = document.getElementById('fromDateError');
        const toDateError = document.getElementById('toDateError');
        
        // Clear previous errors
        this.clearDateValidationErrors();
        
        const now = new Date();
        const fromDate = fromDateInput.value ? new Date(fromDateInput.value) : null;
        const toDate = toDateInput.value ? new Date(toDateInput.value) : null;
        
        let isValid = true;
        
        // Check if from date is in the future
        if (fromDate && fromDate > now) {
            fromDateInput.classList.add('is-invalid');
            fromDateError.textContent = 'From date cannot be in the future.';
            fromDateError.style.display = 'block';
            isValid = false;
        }
        
        // Check if to date is in the future
        if (toDate && toDate > now) {
            toDateInput.classList.add('is-invalid');
            toDateError.textContent = 'To date cannot be in the future.';
            toDateError.style.display = 'block';
            isValid = false;
        }
        
        // Check if from date is greater than to date
        if (fromDate && toDate && fromDate > toDate) {
            fromDateInput.classList.add('is-invalid');
            toDateInput.classList.add('is-invalid');
            fromDateError.textContent = 'From date must be earlier than To date.';
            toDateError.textContent = 'To date must be later than From date.';
            fromDateError.style.display = 'block';
            toDateError.style.display = 'block';
            isValid = false;
        }
        
        return isValid;
    }

    clearDateValidationErrors() {
        const fromDateInput = document.getElementById('fromDate');
        const toDateInput = document.getElementById('toDate');
        const fromDateError = document.getElementById('fromDateError');
        const toDateError = document.getElementById('toDateError');
        
        fromDateInput.classList.remove('is-invalid');
        toDateInput.classList.remove('is-invalid');
        fromDateError.style.display = 'none';
        toDateError.style.display = 'none';
        fromDateError.textContent = '';
        toDateError.textContent = '';
    }

    // Audit Log Search date validation functions
    validateAuditDateRange() {
        const fromDateInput = document.getElementById('auditFromDate');
        const toDateInput = document.getElementById('auditToDate');
        const fromDateError = document.getElementById('auditFromDateError');
        const toDateError = document.getElementById('auditToDateError');
        
        // Clear previous errors
        this.clearAuditDateValidationErrors();
        
        const today = new Date();
        today.setHours(23, 59, 59, 999); // Set to end of today for comparison
        
        const fromDate = fromDateInput.value ? new Date(fromDateInput.value) : null;
        const toDate = toDateInput.value ? new Date(toDateInput.value) : null;
        
        let isValid = true;
        
        // Check if from date is in the future
        if (fromDate && fromDate > today) {
            fromDateInput.classList.add('is-invalid');
            fromDateError.textContent = 'From date cannot be in the future.';
            fromDateError.style.display = 'block';
            isValid = false;
        }
        
        // Check if to date is in the future
        if (toDate && toDate > today) {
            toDateInput.classList.add('is-invalid');
            toDateError.textContent = 'To date cannot be in the future.';
            toDateError.style.display = 'block';
            isValid = false;
        }
        
        // Check if from date is greater than to date
        if (fromDate && toDate && fromDate > toDate) {
            fromDateInput.classList.add('is-invalid');
            toDateInput.classList.add('is-invalid');
            fromDateError.textContent = 'From date must be earlier than To date.';
            toDateError.textContent = 'To date must be later than From date.';
            fromDateError.style.display = 'block';
            toDateError.style.display = 'block';
            isValid = false;
        }
        
        return isValid;
    }

    clearAuditDateValidationErrors() {
        const fromDateInput = document.getElementById('auditFromDate');
        const toDateInput = document.getElementById('auditToDate');
        const fromDateError = document.getElementById('auditFromDateError');
        const toDateError = document.getElementById('auditToDateError');
        
        fromDateInput.classList.remove('is-invalid');
        toDateInput.classList.remove('is-invalid');
        fromDateError.style.display = 'none';
        toDateError.style.display = 'none';
        fromDateError.textContent = '';
        toDateError.textContent = '';
    }

    resetAuditLogSearchForm() {
        // Reset all form fields
        document.getElementById('auditLogSearchForm').reset();
        
        // Clear audit date validation errors
        this.clearAuditDateValidationErrors();
        
        // Show confirmation
        console.log('Audit log search form reset to default values');
    }

    showError(message) {
        // Simple error display - you can enhance this
        console.error(message);
        alert(message);
    }

    // Debug function to check container positions
    debugContainerPositions() {
        console.log('=== DEBUG: Container Position Check ===');
        
        const containers = ['topUserActivitiesContainer'];
        
        containers.forEach(containerId => {
            const container = document.getElementById(containerId);
            if (container) {
                const rect = container.getBoundingClientRect();
                const styles = window.getComputedStyle(container);
                
                console.log(`${containerId}:`, {
                    exists: true,
                    visible: container.offsetParent !== null,
                    position: {
                        top: rect.top,
                        left: rect.left,
                        width: rect.width,
                        height: rect.height,
                        bottom: rect.bottom,
                        right: rect.right
                    },
                    styles: {
                        display: styles.display,
                        visibility: styles.visibility,
                        position: styles.position,
                        overflow: styles.overflow,
                        zIndex: styles.zIndex
                    },
                    content: container.innerHTML.substring(0, 100) + '...'
                });
                
                // Check if container is outside viewport
                if (rect.top < 0 || rect.left < 0 || rect.top > window.innerHeight || rect.left > window.innerWidth) {
                    console.warn(`${containerId} appears to be outside viewport!`);
                }
            } else {
                console.error(`${containerId} not found!`);
            }
        });
        
        console.log('=== End Container Debug ===');
    }

    // Debug function - can be called from browser console
    async debugLoadData() {
        console.log('=== DEBUG: Manual data load test ===');
        try {
            const response = await fetch('/api/log-analytics/dashboard');
            if (!response.ok) {
                console.error('API request failed:', response.status, response.statusText);
                return;
            }
            
            const data = await response.json();
            console.log('Raw API data:', data);
            
            // Test each update method individually
            console.log('Testing updateMetrics...');
            this.updateMetrics(data.statistics);
            
            console.log('Testing updateRecentLogs...');
            this.updateRecentLogs(data.recentLogs);
            
            console.log('Testing updateRecentAuditLogs...');
            this.updateRecentAuditLogs(data.recentAuditLogs);
            
            console.log('Testing updateTopUserActivities...');
            this.updateTopUserActivities(data.topUserActivities);
            
            console.log('Testing updateTopErrors...');
            this.updateTopErrors(data.topErrors);
            
            console.log('=== DEBUG: Test completed ===');
        } catch (error) {
            console.error('Debug test failed:', error);
        }
    }

    // Pagination Methods
    previousPage(section) {
        console.log('=== PREVIOUS PAGE CALLED ===');
        console.log('Section:', section);
        console.log('Pagination in progress:', this.paginationInProgress);
        
        if (this.paginationInProgress) {
            console.log('Pagination already in progress, ignoring request');
            return;
        }
        
        this.paginationInProgress = true;
        const pagData = this.paginationData[section];
        
        // Handle server-side pagination for logs
        if (section === 'recentLogs' || section === 'recentAuditLogs') {
            if (pagData.currentSkip >= pagData.take) {
                if (section === 'recentLogs') {
                    // Check if search is active for recent logs
                    if (this.recentLogsSearchContext && this.recentLogsSearchContext.isActive) {
                        const currentPage = Math.floor(pagData.currentSkip / pagData.take) + 1;
                        this.searchRecentLogs(currentPage - 1);
                    } else {
                        const newSkip = pagData.currentSkip - pagData.take;
                        this.loadRecentLogsPaginated(newSkip, pagData.take);
                    }
                } else {
                    const newSkip = pagData.currentSkip - pagData.take;
                    this.loadRecentAuditLogsPaginated(newSkip, pagData.take);
                }
            }
        } else {
            // Client-side pagination for other sections
            if (pagData.currentPage > 1) {
                pagData.currentPage--;
                this.renderPaginatedContent(section);
            }
        }
        
        this.paginationInProgress = false;
    }

    nextPage(section) {
        console.log('=== NEXT PAGE CALLED ===');
        console.log('Section:', section);
        console.log('Pagination in progress:', this.paginationInProgress);
        
        if (this.paginationInProgress) {
            console.log('Pagination already in progress, ignoring request');
            return;
        }
        
        this.paginationInProgress = true;
        const pagData = this.paginationData[section];
        
        // Handle server-side pagination for logs
        if (section === 'recentLogs' || section === 'recentAuditLogs') {
            if (pagData.hasMore) {
                if (section === 'recentLogs') {
                    // Check if search is active for recent logs
                    if (this.recentLogsSearchContext && this.recentLogsSearchContext.isActive) {
                        const currentPage = Math.floor(pagData.currentSkip / pagData.take) + 1;
                        this.searchRecentLogs(currentPage + 1);
                    } else {
                        const newSkip = pagData.currentSkip + pagData.take;
                        this.loadRecentLogsPaginated(newSkip, pagData.take);
                    }
                } else {
                    const newSkip = pagData.currentSkip + pagData.take;
                    this.loadRecentAuditLogsPaginated(newSkip, pagData.take);
                }
            }
        } else {
            // Client-side pagination for other sections
            const availablePages = Math.ceil(pagData.data.length / pagData.itemsPerPage);
            console.log(`${section} nextPage: currentPage=${pagData.currentPage}, availablePages=${availablePages}, dataLength=${pagData.data.length}`);
            
            if (pagData.currentPage < availablePages) {
                pagData.currentPage++;
                this.renderPaginatedContent(section);
            }
        }
        
        this.paginationInProgress = false;
    }

    renderPaginatedContent(section) {
        const pagData = this.paginationData[section];
        const data = pagData.data;
        const startIndex = (pagData.currentPage - 1) * pagData.itemsPerPage;
        const endIndex = startIndex + pagData.itemsPerPage;
        const paginatedData = data.slice(startIndex, endIndex);
        
        console.log(`=== ${section} PAGINATION DEBUG ===`);
        console.log('Total data length:', data.length);
        console.log('Current page:', pagData.currentPage);
        console.log('Items per page:', pagData.itemsPerPage);
        console.log('Start index:', startIndex);
        console.log('End index:', endIndex);
        console.log('Paginated data length:', paginatedData.length);

        // Get container elements
        const containerMap = {
            recentLogs: 'recentLogsContainer',
            recentAuditLogs: 'recentAuditLogsContainer',
            topErrors: 'topErrorsContainer',
            performance: 'performanceContainer',
            topUserActivities: 'topUserActivitiesContainer',
            apiEndpointPerformance: 'apiEndpointsContainer'
        };

        const paginationMap = {
            recentLogs: 'recentLogsPagination',
            recentAuditLogs: 'recentAuditLogsPagination',
            topErrors: 'topErrorsPagination',
            performance: 'performancePagination',
            topUserActivities: 'topUserActivitiesPagination'
        };

        const infoMap = {
            recentLogs: 'recentLogsInfo',
            recentAuditLogs: 'recentAuditLogsInfo',
            topErrors: 'topErrorsInfo',
            performance: 'performanceInfo',
            topUserActivities: 'topUserActivitiesInfo'
        };

        const container = document.getElementById(containerMap[section]);
        const pagination = document.getElementById(paginationMap[section]);
        const info = document.getElementById(infoMap[section]);

        if (!container) {
            console.error(`Container not found: ${containerMap[section]}`);
            return;
        }

        // Handle empty data
        if (!data || data.length === 0) {
            container.innerHTML = '<div class="text-center p-3 text-muted">No data available</div>';
            if (pagination) pagination.style.display = 'none';
            return;
        }

        // Render content based on section type
        let html = '';
        switch (section) {
            case 'recentLogs':
                html = this.renderRecentLogsHtml(paginatedData);
                break;
            case 'recentAuditLogs':
                html = this.renderRecentAuditLogsHtml(paginatedData);
                break;
            case 'topErrors':
                html = this.renderTopErrorsHtml(paginatedData);
                break;
            case 'performance':
                html = this.renderPerformanceHtml(paginatedData);
                break;
            case 'topUserActivities':
                html = this.renderTopUserActivitiesHtml(paginatedData);
                break;
            case 'apiEndpointPerformance':
                html = this.renderApiEndpointPerformanceHtml(paginatedData);
                break;
        }

        container.innerHTML = html;

        // Update pagination info and controls
        // For client-side pagination, use available data
        const availablePages = Math.ceil(data.length / pagData.itemsPerPage);
        const totalCount = this.getTotalCountForSection(section);
        
        if (data.length > pagData.itemsPerPage) {
            const start = startIndex + 1;
            const end = Math.min(endIndex, data.length);
            
            if (info) {
                const itemType = this.getItemTypeName(section);
                // Show total count from database, but indicate we're showing limited data
                info.textContent = `Showing ${start}-${end} of ${data.length} loaded ${itemType} (${totalCount} total)`;
            }
            
            if (pagination) {
                pagination.style.display = 'flex';
                const prevBtn = pagination.querySelector('button:first-of-type');
                const nextBtn = pagination.querySelector('button:last-of-type');
                
                const prevDisabled = pagData.currentPage === 1;
                const nextDisabled = pagData.currentPage >= availablePages;
                
                if (prevBtn) prevBtn.disabled = prevDisabled;
                if (nextBtn) nextBtn.disabled = nextDisabled;
                
                console.log(`${section} pagination: currentPage=${pagData.currentPage}, availablePages=${availablePages}, dataLength=${data.length}, totalCount=${totalCount}`);
                console.log(`Button states - Previous: ${prevDisabled ? 'DISABLED' : 'ENABLED'}, Next: ${nextDisabled ? 'DISABLED' : 'ENABLED'}`);
            }
        } else {
            if (pagination) pagination.style.display = 'none';
        }
    }

    getItemTypeName(section) {
        const typeNames = {
            recentLogs: 'logs',
            recentAuditLogs: 'activities',
            topErrors: 'errors',
            performance: 'operations',
            topUserActivities: 'users'
        };
        return typeNames[section] || 'items';
    }
    
    getTotalCountForSection(section) {
        // Return actual total counts from dashboard statistics instead of received data length
        console.log('=== GET TOTAL COUNT DEBUG ===');
        console.log('Section:', section);
        console.log('Dashboard Stats:', this.dashboardStats);
        
        let totalCount;
        switch (section) {
            case 'recentLogs':
                totalCount = this.dashboardStats?.totalLogs || 0;
                console.log('Recent Logs Total:', totalCount);
                break;
            case 'recentAuditLogs':
                totalCount = this.dashboardStats?.auditStatistics?.totalAuditLogs || 0;
                console.log('Recent Audit Logs Total:', totalCount);
                break;
            default:
                // For other sections, fall back to data length since they don't have total counts
                const pagData = this.paginationData[section];
                totalCount = pagData?.data?.length || 0;
                console.log('Other section data length:', totalCount);
                break;
        }
        
        console.log('Returning total count:', totalCount);
        return totalCount;
    }

    renderRecentLogsHtml(logs) {
        return logs.map(log => {
            const timestamp = log.timestamp || log.executionTime || new Date().toISOString();
            const level = log.level || (log.hasException ? 'Error' : 'Information');
            const message = log.message || `${log.serviceName || 'Unknown'}.${log.methodName || 'Unknown'}`;
            const isFailedOperation = log.hasException || log.exception;
            
            return `
                <div class="log-entry">
                    <div class="d-flex justify-content-between align-items-start mb-2">
                        <div class="d-flex align-items-center gap-2">
                            <span class="log-level ${level.toLowerCase()}">${level}</span>
                            ${isFailedOperation ? '<span class="badge bg-danger">Failed Operation</span>' : '<span class="badge bg-success">Success</span>'}
                        </div>
                        <span class="log-timestamp">${this.formatDateTime(timestamp)}</span>
                    </div>
                    <div class="log-message">${this.escapeHtml(message)}</div>
                    ${(log.exception && isFailedOperation) ? `
                        <div class="exception-details mt-2">
                            <details class="bg-light border rounded p-2">
                                <summary class="text-danger fw-bold cursor-pointer">
                                    <i class="fas fa-exclamation-triangle me-2"></i>Exception Details
                                </summary>
                                <div class="mt-2">
                                    <pre class="text-danger small mb-0" style="white-space: pre-wrap;">${this.escapeHtml(log.exception)}</pre>
                                </div>
                            </details>
                        </div>
                    ` : ''}
                    <div class="log-meta">
                        <span class="log-application">${log.application || 'ERPPlatform'}</span>
                        ${log.userId ? `<span>User: ${log.userId}</span>` : ''}
                        ${log.httpStatusCode ? `<span>Status: ${log.httpStatusCode}</span>` : ''}
                        ${log.executionDuration ? `<span>Duration: ${log.executionDuration}ms</span>` : ''}
                    </div>
                </div>
            `;
        }).join('');
    }

    renderRecentAuditLogsHtml(logs) {
        return logs.map(log => `
            <div class="audit-log-entry">
                <div class="d-flex justify-content-between align-items-start mb-2">
                    <div class="audit-operation">
                        <strong>${log.serviceName || 'Unknown'}.${log.methodName || 'Unknown'}</strong>
                        ${log.hasException ? '<span class="badge bg-danger ms-2">Error</span>' : '<span class="badge bg-success ms-2">Success</span>'}
                    </div>
                    <span class="audit-timestamp">${this.formatDateTime(log.executionTime)}</span>
                </div>
                <div class="audit-details">
                    ${log.userName ? `<span class="audit-user"><i class="fas fa-user"></i> ${log.userName}</span>` : ''}
                    <span class="audit-duration ${this.getPerformanceClass(log.executionDuration || 0)}">${log.executionDuration || 0}ms</span>
                    <span class="audit-ip"><i class="fas fa-globe"></i> ${log.clientIpAddress || 'Unknown'}</span>
                    <span class="audit-http">${log.httpMethod || 'Unknown'} ${log.httpStatusCode || 0}</span>
                </div>
                ${log.hasException && log.exception ? `<div class="audit-exception">${this.escapeHtml(log.exception)}</div>` : ''}
            </div>
        `).join('');
    }

    renderTopErrorsHtml(errors) {
        return errors.map((error, index) => `
            <div class="d-flex align-items-center py-2 ${index > 0 ? 'border-top' : ''}">
                <div class="flex-shrink-0 me-3">
                    <div class="badge bg-danger fs-6 fw-bold">${error.count || 0}</div>
                </div>
                <div class="flex-grow-1 min-w-0">
                    <div class="fw-semibold text-truncate mb-1" title="${this.escapeHtml(error.errorMessage || '')}">
                        ${this.escapeHtml(error.errorMessage?.substring(0, 60) + (error.errorMessage?.length > 60 ? '...' : '') || 'Unknown Error')}
                    </div>
                    <div class="d-flex gap-3 small text-muted">
                        ${error.exceptionType ? `<span><i class="fas fa-tag me-1"></i>${error.exceptionType}</span>` : ''}
                        <span><i class="fas fa-clock me-1"></i>${this.formatDateTime(error.lastOccurrence)}</span>
                        <span><i class="fas fa-server me-1"></i>${error.affectedApplications ? error.affectedApplications.join(', ') : 'Unknown'}</span>
                    </div>
                </div>
            </div>
        `).join('');
    }

    renderPerformanceHtml(metrics) {
        return metrics.map(metric => `
            <div class="performance-item">
                <div class="performance-operation">${this.escapeHtml(metric.operation)}</div>
                <div class="performance-metrics">
                    <span class="performance-duration ${this.getPerformanceClass(metric.avgDuration)}">
                        ${Math.round(metric.avgDuration)}ms avg
                    </span>
                    <span>Max: ${Math.round(metric.maxDuration)}ms</span>
                    <span>Exec: ${metric.executionCount}</span>
                    ${metric.slowExecutions > 0 ? `<span class="text-warning">Slow: ${metric.slowExecutions}</span>` : ''}
                </div>
            </div>
        `).join('');
    }

    renderTopUserActivitiesHtml(activities) {
        return activities.map((activity, index) => `
            <div class="d-flex align-items-center py-2 ${index > 0 ? 'border-top' : ''}">
                <div class="flex-shrink-0 me-3">
                    <div class="d-flex align-items-center justify-content-center bg-primary text-white rounded-circle" style="width: 40px; height: 40px;">
                        <i class="fas fa-user"></i>
                    </div>
                </div>
                <div class="flex-grow-1 min-w-0">
                    <div class="d-flex justify-content-between align-items-center mb-1">
                        <div class="fw-semibold text-truncate" title="${activity.userName || 'Anonymous'}">
                            ${activity.userName || 'Anonymous'}
                        </div>
                        <div class="badge bg-secondary">${activity.activityCount || 0}</div>
                    </div>
                    <div class="d-flex gap-3 small text-muted">
                        <span><i class="fas fa-check-circle me-1 text-success"></i>${(activity.activityCount || 0) - (activity.failedOperations || 0)} success</span>
                        ${(activity.failedOperations || 0) > 0 ? `<span><i class="fas fa-times-circle me-1 text-danger"></i>${activity.failedOperations} failed</span>` : ''}
                        <span><i class="fas fa-clock me-1"></i>${Math.round(activity.avgExecutionTime || 0)}ms avg</span>
                    </div>
                </div>
            </div>
        `).join('');
    }

    updateSystemHealth(statistics) {
        const statusIndicator = document.getElementById('systemStatusIndicator');
        const statusText = document.getElementById('systemStatusText');
        const lastHealthCheck = document.getElementById('lastHealthCheck');
        const avgResponseTime = document.getElementById('avgResponseTime');
        const recentErrorsCount = document.getElementById('recentErrorsCount');
        const slowOperationsCount = document.getElementById('slowOperationsCount');
        const dbConnectionsStatus = document.getElementById('dbConnectionsStatus');

        // Determine system health based on metrics
        const errorCount = statistics.errorCount || 0;
        const slowOps = statistics.slowOperations || 0;
        const avgResponse = Math.round(statistics.avgResponseTime || 0);
        
        let status = 'Healthy';
        let statusClass = 'bg-success';
        
        if (errorCount > 5 || slowOps > 10 || avgResponse > 1000) {
            status = 'Critical';
            statusClass = 'bg-danger';
        } else if (errorCount > 0 || slowOps > 0 || avgResponse > 500) {
            status = 'Warning';
            statusClass = 'bg-warning';
        }

        if (statusIndicator) {
            statusIndicator.className = `status-indicator ${statusClass}`;
        }
        if (statusText) {
            statusText.textContent = status;
            statusText.className = `fw-semibold text-${statusClass.replace('bg-', '')}`;
        }
        if (lastHealthCheck) {
            lastHealthCheck.textContent = `Last check: ${new Date().toLocaleTimeString()}`;
        }
        if (avgResponseTime) {
            avgResponseTime.textContent = `${avgResponse}ms`;
            avgResponseTime.className = `badge ${avgResponse > 500 ? 'bg-warning' : 'bg-primary'}`;
        }
        if (recentErrorsCount) {
            recentErrorsCount.textContent = errorCount;
            recentErrorsCount.className = `badge ${errorCount > 0 ? 'bg-danger' : 'bg-success'}`;
        }
        if (slowOperationsCount) {
            slowOperationsCount.textContent = slowOps;
        }
        if (dbConnectionsStatus) {
            dbConnectionsStatus.textContent = 'Active';
        }
    }

    updateApiEndpoints(auditStatistics) {
        const container = document.querySelector('#apiEndpointsContainer .api-endpoints');
        if (!container) return;

        // Create mock API endpoint data based on audit statistics
        const endpoints = [
            {
                method: 'GET',
                path: '/api/log-analytics/dashboard',
                calls: Math.floor(auditStatistics.totalAuditLogs * 0.3),
                avgDuration: Math.round(auditStatistics.avgExecutionDuration * 0.8),
                status: 200
            },
            {
                method: 'POST',
                path: '/api/log-analytics/search',
                calls: Math.floor(auditStatistics.totalAuditLogs * 0.2),
                avgDuration: Math.round(auditStatistics.avgExecutionDuration * 1.2),
                status: 200
            },
            {
                method: 'POST',
                path: '/api/log-analytics/audit-search',
                calls: Math.floor(auditStatistics.totalAuditLogs * 0.15),
                avgDuration: Math.round(auditStatistics.avgExecutionDuration * 1.1),
                status: 200
            },
            {
                method: 'GET',
                path: '/api/account/profile',
                calls: auditStatistics.uniqueUsers * 5,
                avgDuration: 45,
                status: 200
            },
            {
                method: 'POST',
                path: '/connect/token',
                calls: auditStatistics.uniqueUsers * 3,
                avgDuration: 120,
                status: 200
            }
        ];

        const html = endpoints.map(endpoint => `
            <div class="api-endpoint-item">
                <div class="d-flex align-items-center justify-content-between mb-2">
                    <div class="d-flex align-items-center gap-2">
                        <span class="endpoint-method ${endpoint.method.toLowerCase()}">${endpoint.method}</span>
                        <span class="endpoint-path">${endpoint.path}</span>
                    </div>
                    <span class="badge bg-secondary">${endpoint.calls}</span>
                </div>
                <div class="endpoint-stats">
                    <div class="endpoint-stat">
                        <i class="fas fa-clock"></i>
                        <span>${endpoint.avgDuration}ms avg</span>
                    </div>
                    <div class="endpoint-stat">
                        <i class="fas fa-check-circle text-success"></i>
                        <span>${endpoint.status}</span>
                    </div>
                </div>
            </div>
        `).join('');

        container.innerHTML = html;
    }

    updateApiEndpointPerformance(auditStatistics) {
        // Create API endpoint performance data from audit statistics
        const endpointData = [
            {
                method: 'GET',
                path: '/api/log-analytics/dashboard',
                calls: Math.floor(auditStatistics.totalAuditLogs * 0.35),
                avgDuration: Math.round(auditStatistics.avgExecutionDuration * 0.7),
                successRate: 98.5,
                status: 'Healthy'
            },
            {
                method: 'POST',
                path: '/api/log-analytics/search',
                calls: Math.floor(auditStatistics.totalAuditLogs * 0.25),
                avgDuration: Math.round(auditStatistics.avgExecutionDuration * 1.3),
                successRate: 97.2,
                status: 'Healthy'
            },
            {
                method: 'POST',
                path: '/api/log-analytics/audit-search',
                calls: Math.floor(auditStatistics.totalAuditLogs * 0.20),
                avgDuration: Math.round(auditStatistics.avgExecutionDuration * 1.1),
                successRate: 96.8,
                status: 'Healthy'
            },
            {
                method: 'GET',
                path: '/api/account/profile',
                calls: auditStatistics.uniqueUsers * 12,
                avgDuration: 45,
                successRate: 99.1,
                status: 'Excellent'
            },
            {
                method: 'POST',
                path: '/connect/token',
                calls: auditStatistics.uniqueUsers * 8,
                avgDuration: 120,
                successRate: 94.5,
                status: 'Warning'
            },
            {
                method: 'GET',
                path: '/api/user/permissions',
                calls: auditStatistics.uniqueUsers * 6,
                avgDuration: 35,
                successRate: 99.5,
                status: 'Excellent'
            }
        ];

        this.paginationData.apiEndpointPerformance.data = endpointData;
        this.paginationData.apiEndpointPerformance.currentPage = 1;
        this.renderPaginatedContent('apiEndpointPerformance');
    }
    
    renderApiEndpointPerformanceHtml(endpoints) {
        if (!endpoints || endpoints.length === 0) {
            return '<div class="text-center text-muted p-4">No API endpoint data available</div>';
        }
        
        return endpoints.map(endpoint => {
            const statusClass = endpoint.status.toLowerCase();
            const isHighTraffic = endpoint.calls > 50;
            
            return `
                <div class="log-entry">
                    <div class="d-flex justify-content-between align-items-start mb-2">
                        <div class="d-flex align-items-center gap-2">
                            <span class="endpoint-method ${endpoint.method.toLowerCase()}">${endpoint.method}</span>
                            ${isHighTraffic ? '<span class="badge bg-info">High Traffic</span>' : '<span class="badge bg-secondary">Standard</span>'}
                            <span class="badge bg-${this.getStatusBadgeColor(endpoint.status)}">${endpoint.status}</span>
                        </div>
                        <span class="log-timestamp">${endpoint.calls} calls</span>
                    </div>
                    <div class="log-message">${this.escapeHtml(endpoint.path)}</div>
                    <div class="log-meta">
                        <span class="log-application"><i class="fas fa-clock"></i> ${endpoint.avgDuration}ms avg</span>
                        <span class="log-application"><i class="fas fa-check-circle"></i> ${endpoint.successRate}% success</span>
                        <span class="log-timestamp">Performance: ${endpoint.status}</span>
                    </div>
                </div>
            `;
        }).join('');
    }
    
    getStatusBadgeColor(status) {
        switch (status.toLowerCase()) {
            case 'excellent': return 'success';
            case 'healthy': return 'primary';
            case 'warning': return 'warning';
            case 'critical': return 'danger';
            default: return 'secondary';
        }
    }
    
    generateSampleLogs(template, count) {
        const levels = ['Information', 'Warning', 'Error'];
        const messages = [
            'User authentication successful',
            'API request processed successfully',
            'Database query executed',
            'File upload completed',
            'Email notification sent',
            'Cache refresh initiated',
            'Session timeout warning',
            'Configuration update applied',
            'Backup process started',
            'System health check performed'
        ];
        
        const sampleLogs = [];
        for (let i = 0; i < count; i++) {
            const log = {
                ...template,
                timestamp: new Date(Date.now() - (i * 60000 + Math.random() * 3600000)).toISOString(),
                level: levels[Math.floor(Math.random() * levels.length)],
                message: messages[Math.floor(Math.random() * messages.length)] + ` (#${i + 1})`,
                hasException: Math.random() < 0.1,
                userId: `user${Math.floor(Math.random() * 100) + 1}@example.com`
            };
            sampleLogs.push(log);
        }
        return sampleLogs;
    }
    
    generateSampleAuditLogs(template, count) {
        const services = ['UserService', 'OrderService', 'ProductService', 'PaymentService'];
        const methods = ['Create', 'Update', 'Delete', 'GetById', 'GetList'];
        const httpMethods = ['GET', 'POST', 'PUT', 'DELETE'];
        
        const sampleLogs = [];
        for (let i = 0; i < count; i++) {
            const service = services[Math.floor(Math.random() * services.length)];
            const method = methods[Math.floor(Math.random() * methods.length)];
            
            const log = {
                ...template,
                executionTime: new Date(Date.now() - (i * 60000 + Math.random() * 3600000)).toISOString(),
                serviceName: service,
                methodName: method,
                httpMethod: httpMethods[Math.floor(Math.random() * httpMethods.length)],
                executionDuration: Math.floor(Math.random() * 2000) + 50,
                userId: `user${Math.floor(Math.random() * 100) + 1}@example.com`,
                hasException: Math.random() < 0.05
            };
            sampleLogs.push(log);
        }
        return sampleLogs;
    }
}

// Initialize dashboard when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    window.logDashboard = new LogAnalyticsDashboard();
});