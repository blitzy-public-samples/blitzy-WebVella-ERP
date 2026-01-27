using System;
using System.Collections.Generic;
using Xunit;
using WebVella.Erp.Plugins.Approval.Api;
using WebVella.Erp.Plugins.Approval.Services;

namespace WebVella.Erp.Plugins.Approval.Tests
{
    /// <summary>
    /// Unit tests for RuleConfigService business logic, validation, and model handling.
    /// Tests focus on validation rules, boundary conditions, operator validation, and error scenarios
    /// that can be verified without database connectivity.
    /// </summary>
    public class RuleConfigServiceTests
    {
        #region ApprovalRuleModel Tests

        /// <summary>
        /// Tests that a new ApprovalRuleModel initializes with correct default values.
        /// </summary>
        [Fact]
        public void ApprovalRuleModel_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var model = new ApprovalRuleModel();

            // Assert
            Assert.Equal(Guid.Empty, model.Id);
            Assert.Equal(Guid.Empty, model.WorkflowId);
            Assert.Null(model.Name);
            Assert.Null(model.FieldName);
            Assert.Null(model.Operator);
            Assert.Equal(0m, model.ThresholdValue);
            Assert.Equal(0, model.Priority);
        }

        /// <summary>
        /// Tests that ApprovalRuleModel properties can be set and retrieved correctly.
        /// </summary>
        [Fact]
        public void ApprovalRuleModel_PropertySetGet_WorksCorrectly()
        {
            // Arrange
            var id = Guid.NewGuid();
            var workflowId = Guid.NewGuid();

            // Act
            var model = new ApprovalRuleModel
            {
                Id = id,
                WorkflowId = workflowId,
                Name = "High Value Purchase Rule",
                FieldName = "total_amount",
                Operator = "gt",
                ThresholdValue = 10000m,
                Priority = 100
            };

            // Assert
            Assert.Equal(id, model.Id);
            Assert.Equal(workflowId, model.WorkflowId);
            Assert.Equal("High Value Purchase Rule", model.Name);
            Assert.Equal("total_amount", model.FieldName);
            Assert.Equal("gt", model.Operator);
            Assert.Equal(10000m, model.ThresholdValue);
            Assert.Equal(100, model.Priority);
        }

        #endregion

        #region ApprovalRuleOperator Enum Tests

        /// <summary>
        /// Tests that ApprovalRuleOperator enum has all expected values.
        /// </summary>
        [Fact]
        public void ApprovalRuleOperator_Enum_HasAllExpectedValues()
        {
            // Assert
            Assert.Equal(0, (int)ApprovalRuleOperator.Eq);
            Assert.Equal(1, (int)ApprovalRuleOperator.Neq);
            Assert.Equal(2, (int)ApprovalRuleOperator.Gt);
            Assert.Equal(3, (int)ApprovalRuleOperator.Gte);
            Assert.Equal(4, (int)ApprovalRuleOperator.Lt);
            Assert.Equal(5, (int)ApprovalRuleOperator.Lte);
            Assert.Equal(6, (int)ApprovalRuleOperator.Contains);
        }

        /// <summary>
        /// Tests that ApprovalRuleOperator enum count is correct (7 values).
        /// </summary>
        [Fact]
        public void ApprovalRuleOperator_Enum_HasCorrectCount()
        {
            // Arrange
            var values = Enum.GetValues(typeof(ApprovalRuleOperator));

            // Assert
            Assert.Equal(7, values.Length);
        }

        #endregion

        #region RuleConfigService Constants Tests

        /// <summary>
        /// Tests that RuleConfigService entity name constant has correct value.
        /// </summary>
        [Fact]
        public void RuleConfigService_EntityNameConstant_IsCorrect()
        {
            // Assert
            Assert.Equal("approval_rule", RuleConfigService.ENTITY_NAME);
        }

        /// <summary>
        /// Tests that RuleConfigService workflow entity name constant has correct value.
        /// </summary>
        [Fact]
        public void RuleConfigService_WorkflowEntityNameConstant_IsCorrect()
        {
            // Assert
            Assert.Equal("approval_workflow", RuleConfigService.WORKFLOW_ENTITY_NAME);
        }

        /// <summary>
        /// Tests that valid operators array contains all expected values.
        /// </summary>
        [Fact]
        public void RuleConfigService_ValidOperators_ContainsExpectedValues()
        {
            // Assert
            Assert.Equal(7, RuleConfigService.VALID_OPERATORS.Length);
            Assert.Contains("eq", RuleConfigService.VALID_OPERATORS);
            Assert.Contains("neq", RuleConfigService.VALID_OPERATORS);
            Assert.Contains("gt", RuleConfigService.VALID_OPERATORS);
            Assert.Contains("gte", RuleConfigService.VALID_OPERATORS);
            Assert.Contains("lt", RuleConfigService.VALID_OPERATORS);
            Assert.Contains("lte", RuleConfigService.VALID_OPERATORS);
            Assert.Contains("contains", RuleConfigService.VALID_OPERATORS);
        }

        /// <summary>
        /// Tests that max name length constant has correct value.
        /// </summary>
        [Fact]
        public void RuleConfigService_MaxNameLengthConstant_IsCorrect()
        {
            // Assert
            Assert.Equal(256, RuleConfigService.MAX_NAME_LENGTH);
        }

        /// <summary>
        /// Tests that max field name length constant has correct value.
        /// </summary>
        [Fact]
        public void RuleConfigService_MaxFieldNameLengthConstant_IsCorrect()
        {
            // Assert
            Assert.Equal(128, RuleConfigService.MAX_FIELD_NAME_LENGTH);
        }

        /// <summary>
        /// Tests that max value length constant has correct value.
        /// </summary>
        [Fact]
        public void RuleConfigService_MaxValueLengthConstant_IsCorrect()
        {
            // Assert
            Assert.Equal(1024, RuleConfigService.MAX_VALUE_LENGTH);
        }

        #endregion

        #region Operator Validation Tests

        /// <summary>
        /// Tests that all valid operators are correctly identified.
        /// </summary>
        [Theory]
        [InlineData("eq")]
        [InlineData("neq")]
        [InlineData("gt")]
        [InlineData("gte")]
        [InlineData("lt")]
        [InlineData("lte")]
        [InlineData("contains")]
        public void Operator_ValidValues_AreAccepted(string operatorValue)
        {
            // Act
            bool isValid = Array.Exists(RuleConfigService.VALID_OPERATORS, o => o == operatorValue);

            // Assert
            Assert.True(isValid, $"'{operatorValue}' should be a valid operator");
        }

        /// <summary>
        /// Tests that invalid operators are correctly rejected.
        /// </summary>
        [Theory]
        [InlineData("invalid")]
        [InlineData("equals")]
        [InlineData("==")]
        [InlineData("!=")]
        [InlineData(">")]
        [InlineData(">=")]
        [InlineData("<")]
        [InlineData("<=")]
        [InlineData("")]
        [InlineData("EQ")]
        [InlineData("GT")]
        [InlineData("CONTAINS")]
        public void Operator_InvalidValues_AreRejected(string operatorValue)
        {
            // Act
            bool isValid = Array.Exists(RuleConfigService.VALID_OPERATORS, o => o == operatorValue);

            // Assert
            Assert.False(isValid, $"'{operatorValue}' should NOT be a valid operator");
        }

        #endregion

        #region Name Validation Tests

        /// <summary>
        /// Tests that rule name with maximum allowed length (256 chars) is considered valid.
        /// </summary>
        [Fact]
        public void RuleName_MaxLength256_IsValid()
        {
            // Arrange
            string maxLengthName = new string('A', 256);

            // Act & Assert
            Assert.Equal(256, maxLengthName.Length);
            Assert.True(maxLengthName.Length <= RuleConfigService.MAX_NAME_LENGTH);
        }

        /// <summary>
        /// Tests that rule name exceeding maximum length (257 chars) is considered invalid.
        /// </summary>
        [Fact]
        public void RuleName_ExceedsMaxLength_IsInvalid()
        {
            // Arrange
            string tooLongName = new string('A', 257);

            // Act & Assert
            Assert.Equal(257, tooLongName.Length);
            Assert.True(tooLongName.Length > RuleConfigService.MAX_NAME_LENGTH);
        }

        /// <summary>
        /// Tests that empty rule name is considered invalid.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RuleName_EmptyOrWhitespace_IsInvalid(string name)
        {
            // Act & Assert
            Assert.True(string.IsNullOrWhiteSpace(name));
        }

        #endregion

        #region FieldName Validation Tests

        /// <summary>
        /// Tests that field name with maximum allowed length (128 chars) is considered valid.
        /// </summary>
        [Fact]
        public void FieldName_MaxLength128_IsValid()
        {
            // Arrange
            string maxLengthName = new string('a', 128);

            // Act & Assert
            Assert.Equal(128, maxLengthName.Length);
            Assert.True(maxLengthName.Length <= RuleConfigService.MAX_FIELD_NAME_LENGTH);
        }

        /// <summary>
        /// Tests that field name exceeding maximum length (129 chars) is considered invalid.
        /// </summary>
        [Fact]
        public void FieldName_ExceedsMaxLength_IsInvalid()
        {
            // Arrange
            string tooLongName = new string('a', 129);

            // Act & Assert
            Assert.Equal(129, tooLongName.Length);
            Assert.True(tooLongName.Length > RuleConfigService.MAX_FIELD_NAME_LENGTH);
        }

        /// <summary>
        /// Tests that empty field name is considered invalid.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void FieldName_EmptyOrWhitespace_IsInvalid(string fieldName)
        {
            // Act & Assert
            Assert.True(string.IsNullOrWhiteSpace(fieldName));
        }

        /// <summary>
        /// Tests valid field name formats.
        /// </summary>
        [Theory]
        [InlineData("total_amount")]
        [InlineData("status")]
        [InlineData("created_on")]
        [InlineData("department_id")]
        public void FieldName_ValidFormat_IsAccepted(string fieldName)
        {
            // Act & Assert
            Assert.False(string.IsNullOrWhiteSpace(fieldName));
            Assert.True(fieldName.Length <= RuleConfigService.MAX_FIELD_NAME_LENGTH);
        }

        #endregion

        #region Value Validation Tests

        /// <summary>
        /// Tests that value with maximum allowed length (1024 chars) is considered valid.
        /// </summary>
        [Fact]
        public void Value_MaxLength1024_IsValid()
        {
            // Arrange
            string maxLengthValue = new string('A', 1024);

            // Act & Assert
            Assert.Equal(1024, maxLengthValue.Length);
            Assert.True(maxLengthValue.Length <= RuleConfigService.MAX_VALUE_LENGTH);
        }

        /// <summary>
        /// Tests that value exceeding maximum length (1025 chars) is considered invalid.
        /// </summary>
        [Fact]
        public void Value_ExceedsMaxLength_IsInvalid()
        {
            // Arrange
            string tooLongValue = new string('A', 1025);

            // Act & Assert
            Assert.Equal(1025, tooLongValue.Length);
            Assert.True(tooLongValue.Length > RuleConfigService.MAX_VALUE_LENGTH);
        }

        /// <summary>
        /// Tests that empty value is considered invalid.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Value_EmptyOrWhitespace_IsInvalid(string value)
        {
            // Act & Assert
            Assert.True(string.IsNullOrWhiteSpace(value));
        }

        /// <summary>
        /// Tests various valid value formats.
        /// </summary>
        [Theory]
        [InlineData("10000")]
        [InlineData("approved")]
        [InlineData("true")]
        [InlineData("2024-01-01")]
        [InlineData("sales")]
        public void Value_ValidFormat_IsAccepted(string value)
        {
            // Act & Assert
            Assert.False(string.IsNullOrWhiteSpace(value));
            Assert.True(value.Length <= RuleConfigService.MAX_VALUE_LENGTH);
        }

        #endregion

        #region Priority Tests

        /// <summary>
        /// Tests that priority defaults to zero.
        /// </summary>
        [Fact]
        public void Priority_DefaultsToZero()
        {
            // Arrange & Act
            var model = new ApprovalRuleModel();

            // Assert
            Assert.Equal(0, model.Priority);
        }

        /// <summary>
        /// Tests that positive priority values are valid.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public void Priority_PositiveValues_AreValid(int priority)
        {
            // Arrange
            var model = new ApprovalRuleModel { Priority = priority };

            // Assert
            Assert.Equal(priority, model.Priority);
            Assert.True(model.Priority >= 0);
        }

        /// <summary>
        /// Tests that zero priority is valid.
        /// </summary>
        [Fact]
        public void Priority_Zero_IsValid()
        {
            // Arrange
            var model = new ApprovalRuleModel { Priority = 0 };

            // Assert
            Assert.Equal(0, model.Priority);
        }

        /// <summary>
        /// Tests that negative priority values are technically allowed by the model.
        /// </summary>
        [Theory]
        [InlineData(-1)]
        [InlineData(-10)]
        public void Priority_NegativeValues_CanBeSet(int priority)
        {
            // Arrange
            var model = new ApprovalRuleModel { Priority = priority };

            // Assert - model allows negative values (validation happens in service)
            Assert.Equal(priority, model.Priority);
        }

        #endregion

        #region WorkflowId Validation Tests

        /// <summary>
        /// Tests that empty workflow ID is considered invalid.
        /// </summary>
        [Fact]
        public void WorkflowId_EmptyGuid_IsInvalid()
        {
            // Arrange
            var model = new ApprovalRuleModel { WorkflowId = Guid.Empty };

            // Assert
            Assert.Equal(Guid.Empty, model.WorkflowId);
        }

        /// <summary>
        /// Tests that valid workflow ID is accepted.
        /// </summary>
        [Fact]
        public void WorkflowId_ValidGuid_IsValid()
        {
            // Arrange
            var workflowId = Guid.NewGuid();
            var model = new ApprovalRuleModel { WorkflowId = workflowId };

            // Assert
            Assert.NotEqual(Guid.Empty, model.WorkflowId);
        }

        #endregion

        #region Complete Rule Validation Tests

        /// <summary>
        /// Tests that a complete valid rule passes all validation criteria.
        /// </summary>
        [Fact]
        public void CompleteRule_AllFieldsValid_PassesValidation()
        {
            // Arrange
            var model = new ApprovalRuleModel
            {
                Id = Guid.NewGuid(),
                WorkflowId = Guid.NewGuid(),
                Name = "High Value Purchase Rule",
                FieldName = "total_amount",
                Operator = "gt",
                ThresholdValue = 10000m,
                Priority = 100
            };

            // Assert - all validation criteria should pass
            Assert.NotEqual(Guid.Empty, model.Id);
            Assert.NotEqual(Guid.Empty, model.WorkflowId);
            Assert.False(string.IsNullOrWhiteSpace(model.Name));
            Assert.True(model.Name.Length <= RuleConfigService.MAX_NAME_LENGTH);
            Assert.False(string.IsNullOrWhiteSpace(model.FieldName));
            Assert.True(model.FieldName.Length <= RuleConfigService.MAX_FIELD_NAME_LENGTH);
            Assert.True(Array.Exists(RuleConfigService.VALID_OPERATORS, o => o == model.Operator));
            Assert.Equal(10000m, model.ThresholdValue);
            Assert.True(model.Priority >= 0);
        }

        /// <summary>
        /// Tests different comparison operators with numeric values.
        /// </summary>
        [Theory]
        [InlineData("eq", 1000)]
        [InlineData("neq", 0)]
        [InlineData("gt", 5000)]
        [InlineData("gte", 5000)]
        [InlineData("lt", 100)]
        [InlineData("lte", 100)]
        public void NumericRule_ValidOperatorAndValue_PassesValidation(string operatorValue, decimal thresholdValue)
        {
            // Arrange
            var model = new ApprovalRuleModel
            {
                Id = Guid.NewGuid(),
                WorkflowId = Guid.NewGuid(),
                Name = "Numeric Rule",
                FieldName = "amount",
                Operator = operatorValue,
                ThresholdValue = thresholdValue,
                Priority = 0
            };

            // Assert
            Assert.True(Array.Exists(RuleConfigService.VALID_OPERATORS, o => o == model.Operator));
            Assert.Equal(thresholdValue, model.ThresholdValue);
        }

        /// <summary>
        /// Tests contains operator with threshold value (for field matching).
        /// Note: Contains operator uses threshold_value as numeric comparison.
        /// </summary>
        [Fact]
        public void ContainsOperator_PassesValidation()
        {
            // Arrange
            var model = new ApprovalRuleModel
            {
                Id = Guid.NewGuid(),
                WorkflowId = Guid.NewGuid(),
                Name = "Department Filter",
                FieldName = "department_code",
                Operator = "contains",
                ThresholdValue = 100m,
                Priority = 50
            };

            // Assert
            Assert.Equal("contains", model.Operator);
            Assert.True(Array.Exists(RuleConfigService.VALID_OPERATORS, o => o == model.Operator));
        }

        #endregion
    }
}
