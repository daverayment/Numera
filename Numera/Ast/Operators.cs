namespace Numera.Ast;

/// <summary>
/// Defines mathematical and logical operators represented within the Numera abstract syntax tree.
/// </summary>
public enum BinaryOperator
{
    /// <summary>Addition ('+').</summary>
    Add,

    /// <summary>Subtraction ('-').</summary>
    Subtract,

    /// <summary>Multiplication ('*').</summary>
    Multiply,

    /// <summary>Division ('/').</summary>
    Divide,

    /// <summary>Remainder/Modulo ('%').</summary>
    Modulo,

    /// <summary>Exponentiation ('^').</summary>
    Power,

    /// <summary>Bitwise left shift ('&lt;&lt;').</summary>
    ShiftLeft,

    /// <summary>Bitwise right shift ('&gt;&gt;').</summary>
    ShiftRight,

    /// <summary>Logical conjunction ('&amp;&amp;', 'and').</summary>
    LogicalAnd,

    /// <summary>Logical disjunction ('||', 'or').</summary>
    LogicalOr,

    /// <summary>Logical exclusive disjunction ('xor').</summary>
    LogicalXor,
}

/// <summary>
/// Defines relational and equality operators used in comparisons.
/// </summary>
public enum ComparisonOperator
{
    /// <summary>Equality ('==').</summary>
    Equal,

    /// <summary>Inequality ('!=', '~=').</summary>
    NotEqual,

    /// <summary>Strictly less than ('&lt;').</summary>
    LessThan,

    /// <summary>Less than or equal to ('&lt;=').</summary>
    LessThanOrEqual,

    /// <summary>Strictly greater than ('&gt;').</summary>
    GreaterThan,

    /// <summary>Greater than or equal to ('&gt;=').</summary>
    GreaterThanOrEqual,
}

/// <summary>
/// Defines unary operators applied to a single operand.
/// </summary>
public enum UnaryOperator
{
    /// <summary>Unary identity / positive sign ('+').</summary>
    Positive,

    /// <summary>Unary negation ('-').</summary>
    Negate,

    /// <summary>Logical negation ('!', 'not').</summary>
    LogicalNot,

    /// <summary>Bitwise complement / NOT ('~').</summary>
    BitwiseNot,
}
