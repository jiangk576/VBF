﻿using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VBF.Compilers.Scanners;
using VBF.Compilers.Parsers;
using RE = VBF.Compilers.Scanners.RegularExpression;
using VBF.Compilers.Parsers.Generator;
using System.IO;
using VBF.Compilers;

namespace Compilers.UnitTests
{
    [TestFixture]
    public class ParserBuilderTest
    {
        [Test]
        public void ProductionInfoManagerTest()
        {
            Lexicon test = new Lexicon();

            var A = test.Lexer.DefineToken(RE.Symbol('a'));
            var D = test.Lexer.DefineToken(RE.Symbol('d'));
            var C = test.Lexer.DefineToken(RE.Symbol('c'));

            Production<object> X = new Production<object>(), Y = new Production<object>(), Z = new Production<object>();

            Z.Rule =
                (from d in D select d as object) |
                (from x in X
                 from y in Y
                 from z in Z
                 select new { x, y, z } as object);

            Y.Rule =
                Grammar.Empty(new object()) |
                (from c in C select c as object);

            X.Rule =
                Y |
                (from a in A select a as object);

            ProductionInfoManager pis = new ProductionInfoManager(Z);

            var xInfo = pis.GetInfo(X);
            var yInfo = pis.GetInfo(Y);
            var zInfo = pis.GetInfo(Z);

            Assert.IsTrue(xInfo.IsNullable, "X should be nullable");
            Assert.IsTrue(yInfo.IsNullable, "Y should be nullable");
            Assert.IsFalse(zInfo.IsNullable, "Z should not be nullable");

            Assert.AreEqual(xInfo.First.Count, 2);
            Assert.AreEqual(xInfo.Follow.Count, 3);

            Assert.IsTrue(xInfo.First.Contains(A.AsTerminal()));
            Assert.IsTrue(xInfo.First.Contains(C.AsTerminal()));

            Assert.IsTrue(xInfo.Follow.Contains(A.AsTerminal()));
            Assert.IsTrue(xInfo.Follow.Contains(C.AsTerminal()));
            Assert.IsTrue(xInfo.Follow.Contains(D.AsTerminal()));

            Assert.AreEqual(yInfo.First.Count, 1);
            Assert.AreEqual(yInfo.Follow.Count, 3);

            Assert.IsTrue(yInfo.First.Contains(C.AsTerminal()));

            Assert.IsTrue(yInfo.Follow.Contains(A.AsTerminal()));
            Assert.IsTrue(yInfo.Follow.Contains(C.AsTerminal()));
            Assert.IsTrue(yInfo.Follow.Contains(D.AsTerminal()));

            Assert.AreEqual(zInfo.First.Count, 3);
            Assert.AreEqual(zInfo.Follow.Count, 0);

            Assert.IsTrue(zInfo.First.Contains(A.AsTerminal()));
            Assert.IsTrue(zInfo.First.Contains(C.AsTerminal()));
            Assert.IsTrue(zInfo.First.Contains(D.AsTerminal()));
        }

        [Test]
        public void ParserDriverConflictTest()
        {
            Lexicon test = new Lexicon();

            var X = test.Lexer.DefineToken(RE.Symbol('x'));
            var PLUS = test.Lexer.DefineToken(RE.Symbol('+'));
            var ASTERISK = test.Lexer.DefineToken(RE.Symbol('*'));

            var scannerinfo = test.CreateScannerInfo();

            Production<object> E = new Production<object>(), T = new Production<object>();

            E.Rule =
                (from e1 in E
                 from plus in PLUS
                 from e2 in E
                 select (object)(((int)e1) + ((int)e2))) |
                (from e1 in E
                 from mul in ASTERISK
                 from e2 in E
                 select (object)(((int)e1) * ((int)e2))) | T;

            T.Rule =
                from x in X
                select (object)2;

            ProductionInfoManager pim = new ProductionInfoManager(E.SuffixedBy(Grammar.Eos()));

            LR0Model lr0 = new LR0Model(pim);
            lr0.BuildModel();

            string dot = lr0.ToString();

            TransitionTable tt = TransitionTable.Create(lr0, scannerinfo);

            ParserDriver driver = new ParserDriver(tt, new SyntaxErrors());

            ForkableScannerBuilder builder = new ForkableScannerBuilder(scannerinfo);
            builder.ErrorManager = new VBF.Compilers.CompilationErrorManager();
            var scanner = builder.Create(new VBF.Compilers.SourceReader(new StringReader("x+x*x")));

            var z1 = scanner.Read();

            driver.Input(z1);

            var z2 = scanner.Read();

            driver.Input(z2);

            var z3 = scanner.Read();

            driver.Input(z3);

            var z4 = scanner.Read();

            driver.Input(z4);

            var z5 = scanner.Read();

            driver.Input(z5);

            var z6 = scanner.Read();

            driver.Input(z6);

            Assert.AreEqual(0, driver.CurrentStackCount);
            Assert.AreEqual(2, driver.AcceptedCount);

            var results = new[] { (int)driver.GetResult(0), (int)driver.GetResult(1) };

            Assert.IsTrue(results.Contains(8));
            Assert.IsTrue(results.Contains(6));
            
        }

        [Test]
        public void ParserDriverSimpleTest()
        {
            Lexicon test = new Lexicon();

            var X = test.Lexer.DefineToken(RE.Symbol('x'));
            var PLUS = test.Lexer.DefineToken(RE.Symbol('+'));

            var scannerinfo = test.CreateScannerInfo();

            Production<object> E = new Production<object>(), T = new Production<object>();

            E.Rule =
                (from t in T
                 from plus in PLUS
                 from e in E
                 select (object)(((int)t) + ((int)e))) | T;

            T.Rule =
                from x in X
                select (object)1;

            ProductionInfoManager pim = new ProductionInfoManager(E.SuffixedBy(Grammar.Eos()));

            LR0Model lr0 = new LR0Model(pim);
            lr0.BuildModel();

            string dot = lr0.ToString();

            TransitionTable tt = TransitionTable.Create(lr0, scannerinfo);

            ParserDriver driver = new ParserDriver(tt, new SyntaxErrors() { TokenUnexpected = 1 });

            ForkableScannerBuilder builder = new ForkableScannerBuilder(scannerinfo);
            builder.ErrorManager = new VBF.Compilers.CompilationErrorManager();
            var scanner = builder.Create(new VBF.Compilers.SourceReader(new StringReader("x+x+")));

            var z1 = scanner.Read();

            driver.Input(z1);

            var z2 = scanner.Read();

            driver.Input(z2);

            var z3 = scanner.Read();

            driver.Input(z3);

            var z4 = scanner.Read();

            driver.Input(z4);

            var z5 = scanner.Read();

            driver.Input(z5);

            var z6 = scanner.Read();

            driver.Input(z6);

            Assert.AreEqual(0, driver.CurrentStackCount);
            Assert.AreEqual(1, driver.AcceptedCount);
            Assert.AreEqual(3, driver.GetResult(0));
        }

        [Test]
        public void WhereGrammaTest()
        {
            Lexicon test = new Lexicon();

            var X = test.Lexer.DefineToken(RE.Symbol('x'));
            var PLUS = test.Lexer.DefineToken(RE.Symbol('+'));
            var GREATER = test.Lexer.DefineToken(RE.Symbol('>'));

            var scannerinfo = test.CreateScannerInfo();

            Production<object> E = new Production<object>(), T = new Production<object>();

            const int EG_SHIFT_RIGHT_SYMBOL = 2003;

            E.Rule =
                (from e in E
                 from g1 in GREATER
                 from g2 in GREATER
                 where Grammar.Check(g2.PrefixTrivia.Count == 0, EG_SHIFT_RIGHT_SYMBOL, new SourceSpan(g1.Span.StartLocation, g2.Span.EndLocation))
                 from t in T
                 select (object)(((int)e) + ((int)t))) | T;

            T.Rule =
                from x in X
                select (object)1;

            ProductionInfoManager pim = new ProductionInfoManager(E.SuffixedBy(Grammar.Eos()));

            LR0Model lr0 = new LR0Model(pim);
            lr0.BuildModel();

            string dot = lr0.ToString();
        }
    }
}