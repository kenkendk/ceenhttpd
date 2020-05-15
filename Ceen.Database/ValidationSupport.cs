using System.Linq;
using System;

namespace Ceen.Database
{
    /// <summary>
    /// Validation exception
    /// </summary>
    public class ValidationException : System.Exception
    {
        /// <summary>
        /// Constructs a new validation exception
        /// </summary>
        public ValidationException() { }
        /// <summary>
        /// Constructs a new validation exception
        /// </summary>
        /// <param name="message">The validation message</param>
        public ValidationException(string message) 
            : base(message) 
        { }
        /// <summary>
        /// Constructs a new validation exception
        /// </summary>
        /// <param name="source">The source attribute</param>
        /// <param name="message">The validation message</param>
        public ValidationException(ValidationBaseAttribute source, string message) 
            : base(message) 
        {
            SourceAttribute = source ?? throw new ArgumentNullException(nameof(source));
        }
        /// <summary>
        /// Constructs a new validation exception
        /// </summary>
        /// <param name="sourceType">The source type</param>
        /// <param name="source">The source attribute</param>
        /// <param name="message">The validation message</param>
        public ValidationException(Type sourceType, ValidationBaseAttribute source, string message)
            : base(message)
        {
            SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
            SourceAttribute = source ?? throw new ArgumentNullException(nameof(source));
        }
        /// <summary>
        /// Constructs a new validation exception
        /// </summary>
        /// <param name="sourceType">The source type</param>
        /// <param name="member">The member</param>
        /// <param name="source">The source attribute</param>
        /// <param name="message">The validation message</param>
        public ValidationException(Type sourceType, System.Reflection.MemberInfo member, ValidationBaseAttribute source, string message)
            : base(message)
        {
            SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
            SourceAttribute = source ?? throw new ArgumentNullException(nameof(source));
            Member = member ?? throw new ArgumentNullException(nameof(member));
        }
        /// <summary>
        /// Constructs a new validation exception
        /// </summary>
        /// <param name="message">The validation message</param>
        /// <param name="inner">The inner exception</param>
        public ValidationException(string message, System.Exception inner) 
            : base(message, inner) 
        { }
        /// <summary>
        /// Constructs a new validation exception
        /// </summary>
        /// <param name="source">The source attribute</param>
        /// <param name="message">The validation message</param>
        /// <param name="inner">The inner exception</param>
        public ValidationException(ValidationBaseAttribute source, string message, System.Exception inner) 
            : base(message, inner) 
        {
            SourceAttribute = source ?? throw new ArgumentNullException(nameof(source));
        }
        /// <summary>
        /// Constructs a new validation exception
        /// </summary>
        /// <param name="sourceType">The source type</param>
        /// <param name="source">The source attribute</param>
        /// <param name="message">The validation message</param>
        /// <param name="inner">The inner exception</param>
        public ValidationException(Type sourceType, ValidationBaseAttribute source, string message, System.Exception inner)
            : base(message, inner)
        {
            SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
            SourceAttribute = source ?? throw new ArgumentNullException(nameof(source));
        }
        /// <summary>
        /// Constructs a new validation exception
        /// </summary>
        /// <param name="sourceType">The source type</param>
        /// <param name="member">The member</param>
        /// <param name="source">The source attribute</param>
        /// <param name="message">The validation message</param>
        /// <param name="inner">The inner exception</param>
        public ValidationException(Type sourceType, System.Reflection.MemberInfo member, ValidationBaseAttribute source, string message, System.Exception inner)
            : base(message, inner)
        {
            SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
            SourceAttribute = source ?? throw new ArgumentNullException(nameof(source));
            Member = member ?? throw new ArgumentNullException(nameof(member));
        }

        /// <summary>
        /// The member this error relates to, if any
        /// </summary>
        public System.Reflection.MemberInfo Member { get; private set; }
        /// <summary>
        /// The type this error relates to, if any
        /// </summary>
        public Type SourceType { get; private set; }
        /// <summary>
        /// The validation attribute that caused the error, if any
        /// </summary>
        public ValidationBaseAttribute SourceAttribute { get; private set; }
    }

    /// <summary>
    /// Support class for basic field validation
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public abstract class ValidationBaseAttribute : Attribute
    {
        /// <summary>
        /// Method that performs the validation and throws a validation exception if the check fails
        /// </summary>
        /// <param name="value">The value to validate</param>
        public abstract void Validate(object value);
    }

    /// <summary>
    /// Rule checking with a function
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public class FunctionRuleAttribute : ValidationBaseAttribute
    {
        /// <summary>
        /// The method to invoke
        /// </summary>
        private readonly Action<object> m_method;

        /// <summary>
        /// Constructs a new function based rule
        /// </summary>
        /// <param name="method">The function used to validate the parameter</param>
        public FunctionRuleAttribute(Action<object> method)
        {
            m_method = method ?? throw new ArgumentNullException(nameof(method));
        }

        /// <summary>
        /// Validates a value by invoking the function
        /// </summary>
        /// <param name="value">The value to validate</param>
        public override void Validate(object value)
        {
            m_method(value);
        }
    }


    /// <summary>
    /// Rule that checks that a string value is not empty
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class StringNotEmptyRuleAttribute : ValidationBaseAttribute
    {
        /// <summary>
        /// Validates that the value is not empty
        /// </summary>
        /// <param name="value">The value to validate</param>
        public override void Validate(object value)
        {
            if (string.IsNullOrWhiteSpace(value as string))
                throw new ValidationException(this, "String must not be empty");
        }
    }

    /// <summary>
    /// Rule that checks that a string value is one of the allowed sets
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class StringInListRuleAttribute : ValidationBaseAttribute
    {
        /// <summary>
        /// Flag indicating if the compare is done with case insensitive
        /// </summary>
        public readonly bool CaseInsensitive;
        /// <summary>
        /// The choices to check for a match
        /// </summary>
        public readonly string[] Choices;

        /// <summary>
        /// Constructs a new choice-list validation rule
        /// </summary>
        /// <param name="choices">The valid choices</param>
        /// <param name="caseInsensitive">Flag indicating if the compare is done with case insensitive</param>
        public StringInListRuleAttribute(string[] choices, bool caseInsensitive = false)
        {
            Choices = choices ?? throw new ArgumentNullException();
            if (choices.Length == 0)
                throw new ArgumentOutOfRangeException(nameof(choices));
            CaseInsensitive = caseInsensitive;
        }

        /// <summary>
        /// Validates that the value is not empty
        /// </summary>
        /// <param name="value">The value to validate</param>
        public override void Validate(object value)
        {
            if (!(value is string))
                throw new ValidationException(this, "Value was not a string");
            if (!Choices.Any(x => string.Equals(x, value as string, CaseInsensitive ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture)))
                throw new ValidationException(this, $"Value must be one of: {string.Join(", ", Choices)}");
        }
    }

    /// <summary>
    /// Rule that checks that a string value has no linefeeds
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class NoNewlinesRuleAttribute : ValidationBaseAttribute
    {
        /// <summary>
        /// Validates that the value has no newlines
        /// </summary>
        /// <param name="value">The value to validate</param>
        public override void Validate(object value)
        {
            if (value == null)
                return;
                
            if (value is string v)
            {
                if (!string.IsNullOrWhiteSpace(v) && v.IndexOfAny(new char[] { '\r', '\n' }) >= 0)
                    throw new ValidationException(this, "String must not contain newline characters");
            }
            else
            {
                throw new ArgumentException("Expected a string");
            }
        }
    }

    /// <summary>
    /// Rule that checks that an integer value is within a certain range
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class IntegerRangeRuleAttribute : ValidationBaseAttribute
    {
        /// <summary>
        /// The minimum value
        /// </summary>
        public readonly long Min;
        /// <summary>
        /// The maximum value
        /// </summary>
        public readonly long Max;

        /// <summary>
        /// Creates a new integer range rule
        /// </summary>
        /// <param name="min">The minimum value (inclusive)</param>
        /// <param name="max">The maximum value (inclusive)</param>
        public IntegerRangeRuleAttribute(long min, long max)
        {
            if (max < min)
                throw new ArgumentException($"{nameof(max)} is smaller than {nameof(min)}");
            Min = min;
            Max = max;
        }

        /// <summary>
        /// Validates that the value is in range
        /// </summary>
        /// <param name="value">The value to validate</param>
        public override void Validate(object value)
        {
            long v;
            ulong uv;

            if (value is int vint)
                v = vint;
            else if (value is long vlong)
                v = vlong;
            else if (value is short vshort)
                v = vshort;
            else if (value is sbyte vsbyte)
                v = vsbyte;
            else
            {
                if (value is uint vuint)
                    uv = vuint;
                else if (value is ulong vulong)
                    uv = vulong;
                else if (value is ushort vushort)
                    uv = vushort;
                else if (value is byte vbyte)
                    uv = vbyte;
                else
                    throw new ArgumentException($"Expected integer value");

                if (uv < (ulong)Min || uv > (ulong)Max)
                    throw new ValidationException(this, $"The value {uv} must be between {Min} and {Max}");

                return;
            }

            if (v < Min || v > Max)
                throw new ValidationException(this, $"The value {v} must be between {Min} and {Max}");
        }
    }

    /// <summary>
    /// Rule that checks that a floating point value is within a certain range
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class FloatRangeRuleAttribute : ValidationBaseAttribute
    {
        /// <summary>
        /// The minimum value
        /// </summary>
        public readonly double Min;
        /// <summary>
        /// The maximum value
        /// </summary>
        public readonly double Max;

        /// <summary>
        /// Creates a new integer range rule
        /// </summary>
        /// <param name="min">The minimum value (inclusive)</param>
        /// <param name="max">The maximum value (inclusive)</param>
        public FloatRangeRuleAttribute(double min, double max)
        {
            if (max < min)
                throw new ArgumentException($"{nameof(max)} is smaller than {nameof(min)}");
            Min = min;
            Max = max;
        }

        /// <summary>
        /// Validates that the value is in range
        /// </summary>
        /// <param name="value">The value to validate</param>
        public override void Validate(object value)
        {
            double v;

            if (value is float vfloat)
                v = vfloat;
            else if (value is double vdouble)
                v = vdouble;
            else
                throw new ArgumentException($"Expected floating point value");

            if (v < Min || v > Max)
                throw new ValidationException(this, $"The value {v} must be between {Min} and {Max}");
        }
    }

    /// <summary>
    /// Rule that checks that a string has a length within the allowed bounds
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class StringLengthRuleAttribute : ValidationBaseAttribute
    {
        /// <summary>
        /// The minimum value
        /// </summary>
        public readonly int Min;
        /// <summary>
        /// The maximum value
        /// </summary>
        public readonly int Max;

        /// <summary>
        /// Creates a string length rule
        /// </summary>
        /// <param name="min">The minimum value (inclusive)</param>
        /// <param name="max">The maximum value (inclusive)</param>
        public StringLengthRuleAttribute(int min, int max)
        {
            if (min < 0)
                throw new ArgumentException($"{nameof(min)} is smaller than zero");
            if (max < min)
                throw new ArgumentException($"{nameof(max)} is smaller than {nameof(min)}");
            Min = min;
            Max = max;
        }

        /// <summary>
        /// Validates that the value is in range
        /// </summary>
        /// <param name="value">The value to validate</param>
        public override void Validate(object value)
        {
            var len = (value as string ?? string.Empty).Length;
            if (len < Min || len > Max)
                throw new ValidationException(this, $"The string length is {len}, but must be between {Min} and {Max}");
        }
    }
}
