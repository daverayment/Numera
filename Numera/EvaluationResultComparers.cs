namespace Numera;

/// <summary>
/// Alternative equality semantics for results whose default equality is numeric.
/// </summary>
public static class EvaluationResultComparers
{
    /// <summary>
    /// Gets a comparer for stored values, context, assignments, and options.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Values include their representation, exactness, and decimal display scale.
    /// Decimal and Double storage is compared bit-for-bit, including signed zero and
    /// Double NaN payloads. Variables are compared by contents and order; names and
    /// definition text use ordinal, case-sensitive equality.
    /// </para>
    /// <para>
    /// Expressions use existing AST equality, ignoring source spans. Options use
    /// existing options equality, including custom-function collection identity.
    /// This does not establish equivalent callback behavior or serialized bytes.
    /// Default-initialized results are supported; missing context or collections remain
    /// distinct from present, empty ones. Hash codes are not persistent identifiers.
    /// </para>
    /// </remarks>
    public static IEqualityComparer<EvaluationResult> Content { get; } = new ContentComparer();

    private sealed class ContentComparer : IEqualityComparer<EvaluationResult>
    {
        public bool Equals(EvaluationResult x, EvaluationResult y) =>
            ValuesEqual(x.Value, y.Value) &&
            ContextsEqual(x.Context, y.Context) &&
            VariablesEqual(x.AssignedVariables, y.AssignedVariables) &&
            object.Equals(x.Options, y.Options);

        public int GetHashCode(EvaluationResult obj)
        {
            var hash = new HashCode();
            AddValue(ref hash, obj.Value);
            hash.Add(obj.Context is not null);
            if (obj.Context is not null)
            {
                AddVariables(ref hash, obj.Context.Variables);
                hash.Add(obj.Context.PreviousResult.HasValue);
                if (obj.Context.PreviousResult is MathValue previous)
                {
                    AddValue(ref hash, previous);
                }

                hash.Add(obj.Context.PreviousExpression);
            }

            AddVariables(ref hash, obj.AssignedVariables);
            hash.Add(obj.Options);
            return hash.ToHashCode();
        }

        private static bool ContextsEqual(EvaluationContext? left, EvaluationContext? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return PreviousResultsEqual(left.PreviousResult, right.PreviousResult) &&
                object.Equals(left.PreviousExpression, right.PreviousExpression) &&
                VariablesEqual(left.Variables, right.Variables);
        }

        private static bool PreviousResultsEqual(MathValue? left, MathValue? right)
        {
            if (left is MathValue first)
            {
                return right is MathValue second && ValuesEqual(first, second);
            }

            return !right.HasValue;
        }

        private static bool VariablesEqual(VariableCollection? left, VariableCollection? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null || left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                var first = left[i];
                var second = right[i];
                if (!StringComparer.Ordinal.Equals(first.Name, second.Name) ||
                    !ValuesEqual(first.Value, second.Value) ||
                    !object.Equals(first.Expression, second.Expression) ||
                    !StringComparer.Ordinal.Equals(first.Definition, second.Definition))
                {
                    return false;
                }
            }

            return true;
        }

        private static void AddVariables(ref HashCode hash, VariableCollection? variables)
        {
            hash.Add(variables is not null);
            if (variables is null)
            {
                return;
            }

            hash.Add(variables.Count);
            foreach (var variable in variables)
            {
                hash.Add(variable.Name, StringComparer.Ordinal);
                AddValue(ref hash, variable.Value);
                hash.Add(variable.Expression);
                hash.Add(variable.Definition, StringComparer.Ordinal);
            }
        }

        private static bool ValuesEqual(MathValue left, MathValue right)
        {
            if (left.Kind != right.Kind || left.Exactness != right.Exactness ||
                left.DecimalDisplayScale != right.DecimalDisplayScale)
            {
                return false;
            }

            if (left.Kind == MathValue.DataType.Double)
            {
                return BitConverter.DoubleToInt64Bits(left.DoubleValue) ==
                    BitConverter.DoubleToInt64Bits(right.DoubleValue);
            }

            if (left.Kind == MathValue.DataType.Decimal)
            {
                Span<int> first = stackalloc int[4];
                Span<int> second = stackalloc int[4];
                decimal.GetBits(left.DecimalValue, first);
                decimal.GetBits(right.DecimalValue, second);
                return first.SequenceEqual(second);
            }

            return left.Equals(right);
        }

        private static void AddValue(ref HashCode hash, MathValue value)
        {
            hash.Add(value.Kind);
            hash.Add(value.Exactness);
            hash.Add(value.DecimalDisplayScale);
            switch (value.Kind)
            {
                case MathValue.DataType.Double:
                    hash.Add(BitConverter.DoubleToInt64Bits(value.DoubleValue));
                    break;
                case MathValue.DataType.Decimal:
                    Span<int> bits = stackalloc int[4];
                    decimal.GetBits(value.DecimalValue, bits);
                    foreach (int part in bits)
                    {
                        hash.Add(part);
                    }

                    break;
                default:
                    hash.Add(value);
                    break;
            }
        }
    }
}