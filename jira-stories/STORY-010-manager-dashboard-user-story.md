# STORY-010: Manager Approval Dashboard with Real-Time Metrics

## Summary

Manager can view real-time dashboard showing team approval metrics (pending count, average time, approval rate, overdue requests) to make faster decisions.

## Description

As a Manager with approval responsibilities,
I want to view a real-time dashboard displaying my team's approval workflow metrics,
so that I can make faster, data-driven decisions about resource allocation and identify processing bottlenecks.

## Acceptance Criteria

- Given I am logged in as a user with Manager role
  When I navigate to the Approvals Dashboard
  Then I see metrics including Pending Approvals Count, Average Approval Time, Approval Rate, and Overdue Requests

- Given the dashboard is displayed
  When 60 seconds have elapsed
  Then the metrics automatically refresh without requiring page reload

- Given I am viewing the dashboard
  When I select a date range filter (7 days, 30 days, or 90 days)
  Then the metrics update to reflect only the selected time period

- Given I am a user without Manager role
  When I attempt to access the Approvals Dashboard
  Then I receive an access denied message
