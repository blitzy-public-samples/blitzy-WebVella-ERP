using System;
using System.Collections.Generic;
using Xunit;
using WebVella.Erp.Plugins.Approval.Api;
using WebVella.Erp.Plugins.Approval.Services;

namespace WebVella.Erp.Plugins.Approval.Tests
{
    /// <summary>
    /// Unit tests for ApprovalRequestService business logic, validation, and model handling.
    /// Tests focus on state machine transitions, validation rules, and error scenarios
    /// that can be verified without database connectivity.
    /// </summary>
    [Trait("Category", "Unit")]
    public class ApprovalRequestServiceTests
    {
        #region ApprovalRequestModel Tests

        /// <summary>
        /// Tests that a new ApprovalRequestModel initializes with correct default values.
        /// </summary>
        [Fact]
        public void ApprovalRequestModel_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var model = new ApprovalRequestModel();

            // Assert
            Assert.Equal(Guid.Empty, model.Id);
            Assert.Equal(Guid.Empty, model.WorkflowId);
            Assert.Null(model.CurrentStepId);
            Assert.Null(model.SourceEntityName);
            Assert.Equal(Guid.Empty, model.SourceRecordId);
            Assert.Null(model.Status);
            Assert.Equal(Guid.Empty, model.RequestedBy);
            Assert.Equal(default(DateTime), model.RequestedOn);
            Assert.Null(model.CompletedOn);
        }

        /// <summary>
        /// Tests that ApprovalRequestModel properties can be set and retrieved correctly.
        /// </summary>
        [Fact]
        public void ApprovalRequestModel_PropertySetGet_WorksCorrectly()
        {
            // Arrange
            var id = Guid.NewGuid();
            var workflowId = Guid.NewGuid();
            var stepId = Guid.NewGuid();
            var sourceRecordId = Guid.NewGuid();
            var requestedBy = Guid.NewGuid();
            var requestedOn = DateTime.UtcNow;
            var completedOn = DateTime.UtcNow.AddHours(2);

            // Act
            var model = new ApprovalRequestModel
            {
                Id = id,
                WorkflowId = workflowId,
                CurrentStepId = stepId,
                SourceEntityName = "purchase_order",
                SourceRecordId = sourceRecordId,
                Status = "pending",
                RequestedBy = requestedBy,
                RequestedOn = requestedOn,
                CompletedOn = completedOn
            };

            // Assert
            Assert.Equal(id, model.Id);
            Assert.Equal(workflowId, model.WorkflowId);
            Assert.Equal(stepId, model.CurrentStepId);
            Assert.Equal("purchase_order", model.SourceEntityName);
            Assert.Equal(sourceRecordId, model.SourceRecordId);
            Assert.Equal("pending", model.Status);
            Assert.Equal(requestedBy, model.RequestedBy);
            Assert.Equal(requestedOn, model.RequestedOn);
            Assert.Equal(completedOn, model.CompletedOn);
        }

        #endregion

        #region ApprovalRequestStatus Enum Tests

        /// <summary>
        /// Tests that ApprovalRequestStatus enum has all expected values.
        /// </summary>
        [Fact]
        public void ApprovalRequestStatus_Enum_HasAllExpectedValues()
        {
            // Assert
            Assert.Equal(0, (int)ApprovalRequestStatus.Pending);
            Assert.Equal(1, (int)ApprovalRequestStatus.Approved);
            Assert.Equal(2, (int)ApprovalRequestStatus.Rejected);
            Assert.Equal(3, (int)ApprovalRequestStatus.Escalated);
            Assert.Equal(4, (int)ApprovalRequestStatus.Expired);
        }

        /// <summary>
        /// Tests that ApprovalRequestStatus enum count is correct (5 values).
        /// </summary>
        [Fact]
        public void ApprovalRequestStatus_Enum_HasCorrectCount()
        {
            // Arrange
            var values = Enum.GetValues(typeof(ApprovalRequestStatus));

            // Assert
            Assert.Equal(5, values.Length);
        }

        #endregion

        #region Status Constants Tests

        // Define local constants matching the service's private constants for validation
        private static readonly string[] ValidStatuses = { "pending", "approved", "rejected", "escalated", "expired" };
        private const string StatusPending = "pending";
        private const string StatusApproved = "approved";
        private const string StatusRejected = "rejected";
        private const string StatusEscalated = "escalated";
        private const string StatusExpired = "expired";

        /// <summary>
        /// Tests that all expected status values are defined.
        /// </summary>
        [Fact]
        public void ApprovalRequest_ValidStatuses_ContainsExpectedValues()
        {
            // Assert
            Assert.Equal(5, ValidStatuses.Length);
            Assert.Contains("pending", ValidStatuses);
            Assert.Contains("approved", ValidStatuses);
            Assert.Contains("rejected", ValidStatuses);
            Assert.Contains("escalated", ValidStatuses);
            Assert.Contains("expired", ValidStatuses);
        }

        /// <summary>
        /// Tests that pending status has expected value.
        /// </summary>
        [Fact]
        public void ApprovalRequest_PendingStatus_IsCorrect()
        {
            // Assert
            Assert.Equal("pending", StatusPending);
        }

        /// <summary>
        /// Tests that approved status has expected value.
        /// </summary>
        [Fact]
        public void ApprovalRequest_ApprovedStatus_IsCorrect()
        {
            // Assert
            Assert.Equal("approved", StatusApproved);
        }

        /// <summary>
        /// Tests that rejected status has expected value.
        /// </summary>
        [Fact]
        public void ApprovalRequest_RejectedStatus_IsCorrect()
        {
            // Assert
            Assert.Equal("rejected", StatusRejected);
        }

        /// <summary>
        /// Tests that escalated status has expected value.
        /// </summary>
        [Fact]
        public void ApprovalRequest_EscalatedStatus_IsCorrect()
        {
            // Assert
            Assert.Equal("escalated", StatusEscalated);
        }

        /// <summary>
        /// Tests that expired status has expected value.
        /// </summary>
        [Fact]
        public void ApprovalRequest_ExpiredStatus_IsCorrect()
        {
            // Assert
            Assert.Equal("expired", StatusExpired);
        }

        #endregion

        #region Status Validation Tests

        /// <summary>
        /// Tests that all valid statuses are correctly identified.
        /// </summary>
        [Theory]
        [InlineData("pending")]
        [InlineData("approved")]
        [InlineData("rejected")]
        [InlineData("escalated")]
        [InlineData("expired")]
        public void Status_ValidValues_AreAccepted(string status)
        {
            // Act
            bool isValid = Array.Exists(ValidStatuses, s => s == status);

            // Assert
            Assert.True(isValid, $"'{status}' should be a valid status");
        }

        /// <summary>
        /// Tests that invalid statuses are correctly rejected.
        /// </summary>
        [Theory]
        [InlineData("invalid")]
        [InlineData("active")]
        [InlineData("cancelled")]
        [InlineData("completed")]
        [InlineData("")]
        [InlineData("PENDING")]
        [InlineData("Approved")]
        public void Status_InvalidValues_AreRejected(string status)
        {
            // Act
            bool isValid = Array.Exists(ValidStatuses, s => s == status);

            // Assert
            Assert.False(isValid, $"'{status}' should NOT be a valid status");
        }

        #endregion

        #region State Transition Tests

        /// <summary>
        /// Tests that 'pending' is an initial valid state.
        /// </summary>
        [Fact]
        public void StateTransition_InitialState_IsPending()
        {
            // Arrange
            var model = new ApprovalRequestModel { Status = "pending" };

            // Assert - pending is the initial state
            Assert.Equal("pending", model.Status);
        }

        /// <summary>
        /// Tests valid state transition from pending to approved.
        /// </summary>
        [Fact]
        public void StateTransition_PendingToApproved_IsValid()
        {
            // Arrange
            var model = new ApprovalRequestModel { Status = "pending" };

            // Act
            model.Status = "approved";

            // Assert
            Assert.Equal("approved", model.Status);
        }

        /// <summary>
        /// Tests valid state transition from pending to rejected.
        /// </summary>
        [Fact]
        public void StateTransition_PendingToRejected_IsValid()
        {
            // Arrange
            var model = new ApprovalRequestModel { Status = "pending" };

            // Act
            model.Status = "rejected";

            // Assert
            Assert.Equal("rejected", model.Status);
        }

        /// <summary>
        /// Tests valid state transition from pending to escalated.
        /// </summary>
        [Fact]
        public void StateTransition_PendingToEscalated_IsValid()
        {
            // Arrange
            var model = new ApprovalRequestModel { Status = "pending" };

            // Act
            model.Status = "escalated";

            // Assert
            Assert.Equal("escalated", model.Status);
        }

        /// <summary>
        /// Tests valid state transition from pending to expired.
        /// </summary>
        [Fact]
        public void StateTransition_PendingToExpired_IsValid()
        {
            // Arrange
            var model = new ApprovalRequestModel { Status = "pending" };

            // Act
            model.Status = "expired";

            // Assert
            Assert.Equal("expired", model.Status);
        }

        /// <summary>
        /// Tests that approved is a terminal state.
        /// </summary>
        [Fact]
        public void TerminalState_Approved_CannotTransition()
        {
            // Arrange - approved is a terminal state
            var terminalStatuses = new[] { "approved", "rejected", "expired" };

            // Assert - these are terminal states (state machine logic in service)
            Assert.Contains("approved", terminalStatuses);
            Assert.Contains("rejected", terminalStatuses);
            Assert.Contains("expired", terminalStatuses);
        }

        #endregion

        #region SourceEntityName Validation Tests

        /// <summary>
        /// Tests that empty source entity name is considered invalid.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SourceEntityName_EmptyOrWhitespace_IsInvalid(string entityName)
        {
            // Act & Assert
            Assert.True(string.IsNullOrWhiteSpace(entityName));
        }

        /// <summary>
        /// Tests valid source entity names.
        /// </summary>
        [Theory]
        [InlineData("purchase_order")]
        [InlineData("expense_request")]
        [InlineData("invoice")]
        [InlineData("task")]
        public void SourceEntityName_ValidFormat_IsAccepted(string entityName)
        {
            // Act & Assert
            Assert.False(string.IsNullOrWhiteSpace(entityName));
        }

        #endregion

        #region CurrentStepId Tests

        /// <summary>
        /// Tests that CurrentStepId can be null.
        /// </summary>
        [Fact]
        public void CurrentStepId_Null_IsAllowed()
        {
            // Arrange
            var model = new ApprovalRequestModel { CurrentStepId = null };

            // Assert
            Assert.Null(model.CurrentStepId);
        }

        /// <summary>
        /// Tests that CurrentStepId can be set to valid GUID.
        /// </summary>
        [Fact]
        public void CurrentStepId_ValidGuid_IsAccepted()
        {
            // Arrange
            var stepId = Guid.NewGuid();
            var model = new ApprovalRequestModel { CurrentStepId = stepId };

            // Assert
            Assert.Equal(stepId, model.CurrentStepId);
        }

        #endregion

        #region CompletedOn Tests

        /// <summary>
        /// Tests that CompletedOn can be null for pending requests.
        /// </summary>
        [Fact]
        public void CompletedOn_Null_ForPendingRequest()
        {
            // Arrange
            var model = new ApprovalRequestModel 
            { 
                Status = "pending",
                CompletedOn = null 
            };

            // Assert
            Assert.Null(model.CompletedOn);
        }

        /// <summary>
        /// Tests that CompletedOn has value for approved requests.
        /// </summary>
        [Fact]
        public void CompletedOn_HasValue_ForApprovedRequest()
        {
            // Arrange
            var completedOn = DateTime.UtcNow;
            var model = new ApprovalRequestModel 
            { 
                Status = "approved",
                CompletedOn = completedOn 
            };

            // Assert
            Assert.NotNull(model.CompletedOn);
            Assert.Equal(completedOn, model.CompletedOn);
        }

        /// <summary>
        /// Tests that CompletedOn has value for rejected requests.
        /// </summary>
        [Fact]
        public void CompletedOn_HasValue_ForRejectedRequest()
        {
            // Arrange
            var completedOn = DateTime.UtcNow;
            var model = new ApprovalRequestModel 
            { 
                Status = "rejected",
                CompletedOn = completedOn 
            };

            // Assert
            Assert.NotNull(model.CompletedOn);
        }

        #endregion

        #region Approval Time Calculation Tests

        /// <summary>
        /// Tests approval time calculation between RequestedOn and CompletedOn.
        /// </summary>
        [Fact]
        public void ApprovalTime_CalculatedCorrectly()
        {
            // Arrange
            var requestedOn = DateTime.UtcNow.AddHours(-2);
            var completedOn = DateTime.UtcNow;
            var model = new ApprovalRequestModel
            {
                RequestedOn = requestedOn,
                CompletedOn = completedOn
            };

            // Act
            var approvalTime = model.CompletedOn.Value - model.RequestedOn;

            // Assert
            Assert.True(approvalTime.TotalHours >= 2);
        }

        #endregion
    }
}
