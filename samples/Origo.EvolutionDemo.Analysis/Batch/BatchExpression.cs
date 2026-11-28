using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Origo.EvolutionDemo.Analysis.Batch;

/// <summary>Small arithmetic expression evaluator for manifest-derived metrics.</summary>
public static class BatchExpression
{
    /// <summary>Evaluates +, -, *, /, parentheses, unary minus, numbers, and previous metric names.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="expression" /> is null.</exception>
    /// <exception cref="InvalidDataException">The expression is malformed or references a missing metric.</exception>
    public static double Evaluate(string expression, IReadOnlyDictionary<string, double> metrics)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(metrics);
        var parser = new Parser(expression, metrics);
        var value = parser.ParseExpression();
        parser.ExpectEnd();
        if (!double.IsFinite(value))
            throw new InvalidDataException($"Expression '{expression}' produced a non-finite value.");
        return value;
    }

    private sealed class Parser
    {
        private readonly string _text;
        private readonly IReadOnlyDictionary<string, double> _metrics;
        private int _position;

        internal Parser(string text, IReadOnlyDictionary<string, double> metrics)
        {
            _text = text;
            _metrics = metrics;
        }

        internal double ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (TryConsume('+'))
                    value += ParseTerm();
                else if (TryConsume('-'))
                    value -= ParseTerm();
                else
                    return value;
            }
        }

        internal void ExpectEnd()
        {
            SkipWhitespace();
            if (_position != _text.Length)
                throw Error($"unexpected '{_text[_position]}'");
        }

        private double ParseTerm()
        {
            var value = ParseFactor();
            while (true)
            {
                SkipWhitespace();
                if (TryConsume('*'))
                    value *= ParseFactor();
                else if (TryConsume('/'))
                {
                    var divisor = ParseFactor();
                    if (divisor == 0d)
                        throw Error("division by zero");
                    value /= divisor;
                }
                else
                    return value;
            }
        }

        private double ParseFactor()
        {
            SkipWhitespace();
            if (TryConsume('+'))
                return ParseFactor();
            if (TryConsume('-'))
                return -ParseFactor();
            if (TryConsume('('))
            {
                var value = ParseExpression();
                SkipWhitespace();
                if (!TryConsume(')'))
                    throw Error("missing ')'");
                return value;
            }

            if (_position < _text.Length && (char.IsAsciiDigit(_text[_position]) || _text[_position] == '.'))
                return ParseNumber();
            if (_position < _text.Length && (char.IsAsciiLetter(_text[_position]) || _text[_position] == '_'))
                return ParseIdentifier();
            throw Error("expected a number, metric name, or '('");
        }

        private double ParseNumber()
        {
            var start = _position;
            while (_position < _text.Length
                && (char.IsAsciiDigit(_text[_position]) || _text[_position] is '.' or 'e' or 'E' or '+' or '-'))
            {
                // A sign is part of the number only immediately after e/E.
                if (_text[_position] is '+' or '-'
                    && (_position == start || _text[_position - 1] is not ('e' or 'E')))
                    break;
                _position++;
            }

            var token = _text[start.._position];
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                || !double.IsFinite(value))
                throw Error($"invalid number '{token}'");
            return value;
        }

        private double ParseIdentifier()
        {
            var start = _position;
            while (_position < _text.Length
                && (char.IsAsciiLetterOrDigit(_text[_position]) || _text[_position] is '_' or '.'))
                _position++;
            var name = _text[start.._position];
            if (!_metrics.TryGetValue(name, out var value))
                throw Error($"unknown or later metric '{name}'");
            return value;
        }

        private bool TryConsume(char expected)
        {
            SkipWhitespace();
            if (_position >= _text.Length || _text[_position] != expected)
                return false;
            _position++;
            return true;
        }

        private void SkipWhitespace()
        {
            while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
                _position++;
        }

        private InvalidDataException Error(string message) =>
            new($"Expression '{_text}' at position {_position}: {message}.");
    }
}
