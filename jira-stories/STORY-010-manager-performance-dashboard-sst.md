# STORY-010: Manager Performance Dashboard (SST Format)

## Summary

Manager Performance Dashboard: Real-time approval workflow metrics enabling data-driven decisions through consolidated KPI views with auto-refresh and date filtering.

## User Story Description

**As a** Manager with approval responsibilities,
**I want** to view a real-time dashboard displaying my team's approval workflow metrics,
**so that** I can make faster, data-driven decisions about resource allocation and identify processing bottlenecks.

## Acceptance Criteria

- [ ] **AC1**: Given I am logged in as a user with Manager role, When I navigate to the Approvals Dashboard page, Then I see a dashboard displaying my team's approval metrics including Pending Approvals Count, Average Approval Time, Approval Rate, Overdue Requests, and Recent Activity

- [ ] **AC2**: Given the dashboard is displayed, When 60 seconds have elapsed, Then the metrics automatically refresh without requiring page reload and the display updates to reflect current data

- [ ] **AC3**: Given I am viewing the dashboard, When I select a date range filter (7 days, 30 days, 90 days, or custom range), Then the metrics update to reflect only the selected time period

- [ ] **AC4**: Given I have pending approval requests in queue where I am an authorized approver, When I view the Pending Approvals metric, Then the count accurately reflects requests awaiting my action

- [ ] **AC5**: Given approval requests exceed their configured timeout from the associated approval step, When I view the Overdue Requests metric, Then the count accurately identifies requests past their SLA

- [ ] **AC6**: Given I am a user without Manager role, When I attempt to access the dashboard, Then I receive an access denied message and am not shown the dashboard metrics

## Story Estimation

**5 Story Points** (Fibonacci)

| Factor | Assessment |
|--------|------------|
| Effort | Medium - Single dashboard component with established patterns |
| Complexity | Medium - Service layer with entity queries |
| Uncertainty | Low - Clear requirements from STORY-009 |

**Comparable Stories:**
- STORY-007 (REST API): 5 points - Similar scope with API endpoints
- STORY-008 (UI Components): 8 points - Higher complexity with 4 components
- STORY-003 (Config Services): 5 points - Similar service layer work

## INVEST Criteria Validation

| Criterion | Status | Evidence |
|-----------|--------|----------|
| **Independent** | ✓ Pass | Self-contained story; builds on completed STORY-007/008 infrastructure |
| **Negotiable** | ✓ Pass | Configurable refresh interval, date ranges, and metrics selection |
| **Valuable** | ✓ Pass | Directly addresses manager decision-making business objective |
| **Estimable** | ✓ Pass | 5 story points based on comparable story analysis |
| **Sized** | ✓ Pass | Single sprint deliverable with established component patterns |
| **Testable** | ✓ Pass | 6 clear Given/When/Then acceptance criteria with pass/fail conditions |

**Demo-ability:** Dashboard with live metrics can be demonstrated to Product Owner showing all 5 KPIs updating in real-time with date filtering.

## Labels

`dashboard`, `metrics`, `ui`, `manager`, `approval`, `real-time`, `sst-format`

## Dependencies

| Story ID | Dependency Type | Description |
|----------|-----------------|-------------|
| STORY-007 | Technical | REST API endpoints for metrics retrieval |
| STORY-008 | Pattern | PageComponent implementation patterns |
| STORY-009 | Reference | Technical implementation details |

## Additional Notes

### Business Value Summary

- **Reduces time managers spend gathering performance data** from multiple sources by providing a unified dashboard view
- **Enables proactive identification of workflow bottlenecks** before escalation through real-time visibility into approval queue health
- **Provides visibility into team workload** for resource planning decisions and capacity management
- **Supports compliance reporting** with real-time SLA monitoring and overdue request tracking
- **Improves manager accountability** through transparent metrics and audit-ready performance data

### Key Technical Features

| Feature | Description |
|---------|-------------|
| Auto-Refresh | Configurable interval (default 60 seconds) without page reload |
| Date Range Filtering | 7 days, 30 days, 90 days, or custom range selection |
| 5 Key Metrics | Pending Approvals, Average Time, Approval Rate, Overdue Requests, Recent Activity |
| Role-Based Access | Manager role validation via SecurityContext |
| PageComponent Pattern | Follows established WebVella ERP component architecture |

### Testing Considerations

- [ ] Verify Manager role access grants dashboard visibility
- [ ] Verify non-Manager role receives access denied message
- [ ] Verify metrics accurately reflect approval_request entity data
- [ ] Verify auto-refresh timer respects configured interval
- [ ] Verify date range filter correctly scopes metric calculations
- [ ] Verify overdue calculation uses approval_step.timeout_hours correctly
- [ ] Verify recent activity displays last 5 approval actions

### Related Documentation

- Technical implementation details: `jira-stories/STORY-009-manager-dashboard-metrics.md`
- API endpoint specification: `jira-stories/STORY-007-approval-rest-api.md`
- UI component patterns: `jira-stories/STORY-008-approval-ui-components.md`

---

*This User Story follows the SST (State Street) User Story Template format with WHO/WHAT/WHY description structure and Given/When/Then acceptance criteria syntax.*
