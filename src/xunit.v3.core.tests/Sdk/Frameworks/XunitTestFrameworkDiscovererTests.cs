﻿using System;
using System.Collections.Generic;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;
using Xunit.Internal;
using Xunit.Runner.Common;
using Xunit.Runner.v2;
using Xunit.Sdk;
using Xunit.v3;

public class XunitTestFrameworkDiscovererTests
{
	public class Construction
	{
		[Fact]
		public static void GuardClause()
		{
			var assembly = Substitute.For<IAssemblyInfo>();
			var sourceProvider = Substitute.For<_ISourceInformationProvider>();
			var diagnosticMessageSink = SpyMessageSink.Create();

			Assert.Throws<ArgumentNullException>("assemblyInfo", () => new XunitTestFrameworkDiscoverer(assemblyInfo: null!, configFileName: null, sourceProvider, diagnosticMessageSink));
			Assert.Throws<ArgumentNullException>("sourceProvider", () => new XunitTestFrameworkDiscoverer(assembly, configFileName: null, sourceProvider: null!, diagnosticMessageSink));
			Assert.Throws<ArgumentNullException>("diagnosticMessageSink", () => new XunitTestFrameworkDiscoverer(assembly, configFileName: null, sourceProvider, diagnosticMessageSink: null!));
		}
	}

	public static class FindByAssembly
	{
		[Fact]
		public static void GuardClauses()
		{
			var framework = TestableXunitTestFrameworkDiscoverer.Create();

			Assert.Throws<ArgumentNullException>("discoveryMessageSink", () => framework.Find(discoveryMessageSink: null!, discoveryOptions: _TestFrameworkOptions.ForDiscovery()));
			Assert.Throws<ArgumentNullException>("discoveryOptions", () => framework.Find(discoveryMessageSink: Substitute.For<_IMessageSink>(), discoveryOptions: null!));
		}

		[Fact]
		public static void AssemblyWithNoTypes_ReturnsNoTestCases()
		{
			var framework = TestableXunitTestFrameworkDiscoverer.Create();

			framework.Find();

			Assert.Empty(framework.TestCases);
		}

		[Fact]
		public static void RequestsOnlyPublicTypesFromAssembly()
		{
			var framework = TestableXunitTestFrameworkDiscoverer.Create(collectionFactory: Substitute.For<IXunitTestCollectionFactory>());

			framework.Find();

			framework.Assembly.Received(1).GetTypes(includePrivateTypes: false);
		}

		[Fact]
		public static void ExcludesAbstractTypesFromDiscovery()
		{
			var abstractClassTypeInfo = Reflector.Wrap(typeof(AbstractClass));
			var assembly = Mocks.AssemblyInfo(types: new[] { abstractClassTypeInfo });
			var framework = Substitute.For<TestableXunitTestFrameworkDiscoverer>(assembly);
			framework.FindTestsForClass(null!).ReturnsForAnyArgs(true);

			framework.Find();
			framework.Sink.Finished.WaitOne();

			framework.Received(0).FindTestsForClass(Arg.Any<_ITestClass>());
		}

		[Fact]
		public static void CallsFindImplWhenTypesAreFoundInAssembly()
		{
			var objectTypeInfo = Reflector.Wrap(typeof(object));
			var intTypeInfo = Reflector.Wrap(typeof(int));
			var assembly = Mocks.AssemblyInfo(types: new[] { objectTypeInfo, intTypeInfo });
			var framework = Substitute.For<TestableXunitTestFrameworkDiscoverer>(assembly);
			framework.FindTestsForClass(null!).ReturnsForAnyArgs(true);

			framework.Find();
			framework.Sink.Finished.WaitOne();

			framework.Received(1).FindTestsForClass(Arg.Is<_ITestClass>(testClass => testClass.Class == objectTypeInfo));
			framework.Received(1).FindTestsForClass(Arg.Is<_ITestClass>(testClass => testClass.Class == intTypeInfo));
		}

		[Fact]
		public static void DoesNotCallSourceProviderWhenNotAskedFor()
		{
			var sourceProvider = Substitute.For<_ISourceInformationProvider>();
			var typeInfo = Reflector.Wrap(typeof(ClassWithSingleTest));
			var mockAssembly = Mocks.AssemblyInfo(types: new[] { typeInfo });
			var framework = TestableXunitTestFrameworkDiscoverer.Create(mockAssembly, sourceProvider);

			framework.Find();
			framework.Sink.Finished.WaitOne();

			sourceProvider.Received(0).GetSourceInformation(Arg.Any<string?>(), Arg.Any<string?>());
		}

		[Fact]
		public static void SendsDiscoveryStartingMessage()
		{
			var typeInfo = Reflector.Wrap(typeof(ClassWithSingleTest));
			var mockAssembly = Mocks.AssemblyInfo(types: new[] { typeInfo });
			var framework = TestableXunitTestFrameworkDiscoverer.Create(mockAssembly);

			framework.Find();
			framework.Sink.Finished.WaitOne();

			Assert.True(framework.Sink.StartSeen);
		}
	}

	public class FindByTypeName
	{
		[Fact]
		public static void GuardClauses()
		{
			var framework = TestableXunitTestFrameworkDiscoverer.Create();
			var typeName = typeof(object).FullName!;
			var sink = Substitute.For<_IMessageSink>();
			var options = _TestFrameworkOptions.ForDiscovery();

			Assert.Throws<ArgumentNullException>("typeName", () => framework.Find(typeName: null!, discoveryMessageSink: sink, discoveryOptions: options));
			Assert.Throws<ArgumentException>("typeName", () => framework.Find(typeName: "", discoveryMessageSink: sink, discoveryOptions: options));
			Assert.Throws<ArgumentNullException>("discoveryMessageSink", () => framework.Find(typeName, discoveryMessageSink: null!, discoveryOptions: options));
			Assert.Throws<ArgumentNullException>("discoveryOptions", () => framework.Find(typeName, discoveryMessageSink: sink, discoveryOptions: null!));
		}

		[Fact]
		public static void RequestsPublicAndPrivateMethodsFromType()
		{
			var framework = TestableXunitTestFrameworkDiscoverer.Create();
			var type = Substitute.For<ITypeInfo>();
			framework.Assembly.GetType("abc").Returns(type);

			framework.Find("abc");
			framework.Sink.Finished.WaitOne();

			type.Received(1).GetMethods(includePrivateMethods: true);
		}

		[Fact]
		public static void CallsFindImplWhenMethodsAreFoundOnType()
		{
			var framework = Substitute.For<TestableXunitTestFrameworkDiscoverer>();
			var type = Substitute.For<ITypeInfo>();
			framework.Assembly.GetType("abc").Returns(type);

			framework.Find("abc");
			framework.Sink.Finished.WaitOne();

			framework.Received(1).FindTestsForClass(Arg.Is<_ITestClass>(testClass => testClass.Class == type));
		}

		[Fact]
		public static void ExcludesAbstractTypesFromDiscovery()
		{
			var framework = Substitute.For<TestableXunitTestFrameworkDiscoverer>();
			var type = Substitute.For<ITypeInfo>();
			type.IsAbstract.Returns(true);
			framework.Assembly.GetType("abc").Returns(type);

			framework.Find("abc");
			framework.Sink.Finished.WaitOne();

			framework.Received(0).FindTestsForClass(Arg.Is<_ITestClass>(testClass => testClass.Class == type));
		}

		[Fact]
		public static void DoesNotCallSourceProviderWhenNotAskedFor()
		{
			var sourceProvider = Substitute.For<_ISourceInformationProvider>();
			var framework = TestableXunitTestFrameworkDiscoverer.Create(sourceProvider: sourceProvider);

			framework.Find("abc");

			sourceProvider.Received(0).GetSourceInformation(Arg.Any<string?>(), Arg.Any<string?>());
		}

		[Fact]
		public static void SendsDiscoveryStartingMessage()
		{
			var typeInfo = Reflector.Wrap(typeof(ClassWithSingleTest));
			var mockAssembly = Mocks.AssemblyInfo(types: new[] { typeInfo });
			var framework = TestableXunitTestFrameworkDiscoverer.Create(mockAssembly);

			framework.Find("abc");
			framework.Sink.Finished.WaitOne();

			Assert.True(framework.Sink.StartSeen);
		}
	}

	public class FindImpl
	{
		class ClassWithNoTests
		{
			public static void NonTestMethod() { }
		}

		[Fact]
		public static void ClassWithNoTests_ReturnsNoTestCases()
		{
			var framework = TestableXunitTestFrameworkDiscoverer.Create();
			var testClass = new TestClass(Mocks.TestCollection(), Reflector.Wrap(typeof(ClassWithNoTests)));

			framework.FindTestsForClass(testClass);

			Assert.False(framework.Sink.Finished.WaitOne(0));
		}

		class ClassWithOneFact
		{
			[Fact]
			public static void TestMethod() { }
		}

		[Fact]
		public static void AssemblyWithFact_ReturnsOneTestCaseOfTypeXunitTestCase()
		{
			var framework = TestableXunitTestFrameworkDiscoverer.Create();
			var testClass = new TestClass(Mocks.TestCollection(), Reflector.Wrap(typeof(ClassWithOneFact)));

			framework.FindTestsForClass(testClass);

			Assert.Collection(
				framework.Sink.TestCases,
				testCase => Assert.IsType<XunitTestCase>(testCase)
			);
		}

		class ClassWithMixOfFactsAndNonFacts
		{
			[Fact]
			public static void TestMethod1() { }

			[Fact]
			public static void TestMethod2() { }

			public static void NonTestMethod() { }
		}

		[Fact]
		public static void AssemblyWithMixOfFactsAndNonTests_ReturnsTestCasesOnlyForFacts()
		{
			var framework = TestableXunitTestFrameworkDiscoverer.Create();
			var testClass = new TestClass(Mocks.TestCollection(), Reflector.Wrap(typeof(ClassWithMixOfFactsAndNonFacts)));

			framework.FindTestsForClass(testClass);

			Assert.Equal(2, framework.Sink.TestCases.Count);
			Assert.Single(framework.Sink.TestCases, t => t.DisplayName == "XunitTestFrameworkDiscovererTests+FindImpl+ClassWithMixOfFactsAndNonFacts.TestMethod1");
			Assert.Single(framework.Sink.TestCases, t => t.DisplayName == "XunitTestFrameworkDiscovererTests+FindImpl+ClassWithMixOfFactsAndNonFacts.TestMethod2");
		}

		class TheoryWithInlineData
		{
			[Theory]
			[InlineData("Hello world")]
			[InlineData(42)]
			public static void TheoryMethod(object value) { }
		}

		[Fact]
		public static void AssemblyWithTheoryWithInlineData_ReturnsOneTestCasePerDataRecord()
		{
			var framework = TestableXunitTestFrameworkDiscoverer.Create();
			var testClass = Mocks.TestClass<TheoryWithInlineData>();

			framework.FindTestsForClass(testClass);

			Assert.Equal(2, framework.Sink.TestCases.Count);
			Assert.Single(framework.Sink.TestCases, t => t.DisplayName == "XunitTestFrameworkDiscovererTests+FindImpl+TheoryWithInlineData.TheoryMethod(value: \"Hello world\")");
			Assert.Single(framework.Sink.TestCases, t => t.DisplayName == "XunitTestFrameworkDiscovererTests+FindImpl+TheoryWithInlineData.TheoryMethod(value: 42)");
		}

		class TheoryWithPropertyData
		{
			public static IEnumerable<object[]> TheData
			{
				get
				{
					yield return new object[] { 42 };
					yield return new object[] { 2112 };
				}
			}

			[Theory]
			[MemberData("TheData")]
			public static void TheoryMethod(int value) { }
		}

		[Fact]
		public static void AssemblyWithTheoryWithPropertyData_ReturnsOneTestCasePerDataRecord()
		{
			var framework = TestableXunitTestFrameworkDiscoverer.Create();
			var testClass = Mocks.TestClass<TheoryWithPropertyData>();

			framework.FindTestsForClass(testClass);

			Assert.Equal(2, framework.Sink.TestCases.Count);
			Assert.Single(framework.Sink.TestCases, testCase => testCase.DisplayName == "XunitTestFrameworkDiscovererTests+FindImpl+TheoryWithPropertyData.TheoryMethod(value: 42)");
			Assert.Single(framework.Sink.TestCases, testCase => testCase.DisplayName == "XunitTestFrameworkDiscovererTests+FindImpl+TheoryWithPropertyData.TheoryMethod(value: 2112)");
		}

		[Fact]
		public static void AssemblyWithMultiLevelHierarchyWithFactOverridenInNonImmediateDerivedClass_ReturnsOneTestCase()
		{
			var framework = TestableXunitTestFrameworkDiscoverer.Create();
			var testClass = Mocks.TestClass<Child>();

			framework.FindTestsForClass(testClass);

			Assert.Equal(1, framework.Sink.TestCases.Count);
			Assert.Equal("XunitTestFrameworkDiscovererTests+FindImpl+Child.FactOverridenInNonImmediateDerivedClass", framework.Sink.TestCases[0].DisplayName);
		}

		public abstract class GrandParent
		{
			[Fact]
			public virtual void FactOverridenInNonImmediateDerivedClass()
			{
				Assert.True(true);
			}
		}

		public abstract class Parent : GrandParent { }

		public class Child : Parent
		{
			public override void FactOverridenInNonImmediateDerivedClass()
			{
				base.FactOverridenInNonImmediateDerivedClass();

				Assert.False(false);
			}
		}
	}

	public class CreateTestClass
	{
		class ClassWithNoCollection
		{
			[Fact]
			public static void TestMethod() { }
		}

		[Fact]
		public static void DefaultTestCollection()
		{
			var framework = TestableXunitTestFrameworkDiscoverer.Create();
			var type = Reflector.Wrap(typeof(ClassWithNoCollection));

			var testClass = framework.CreateTestClass(type);

			Assert.NotNull(testClass.TestCollection);
			Assert.Equal("Test collection for XunitTestFrameworkDiscovererTests+CreateTestClass+ClassWithNoCollection", testClass.TestCollection.DisplayName);
			Assert.Null(testClass.TestCollection.CollectionDefinition);
		}

		[Collection("This a collection without declaration")]
		class ClassWithUndeclaredCollection
		{
			[Fact]
			public static void TestMethod() { }
		}

		[Fact]
		public static void UndeclaredTestCollection()
		{
			var framework = TestableXunitTestFrameworkDiscoverer.Create();
			var type = Reflector.Wrap(typeof(ClassWithUndeclaredCollection));

			var testClass = framework.CreateTestClass(type);

			Assert.NotNull(testClass.TestCollection);
			Assert.Equal("This a collection without declaration", testClass.TestCollection.DisplayName);
			Assert.Null(testClass.TestCollection.CollectionDefinition);
		}

		[CollectionDefinition("This a defined collection")]
		public class DeclaredCollection { }

		[Collection("This a defined collection")]
		class ClassWithDefinedCollection
		{
			[Fact]
			public static void TestMethod() { }
		}

		[Fact]
		public static void DefinedTestCollection()
		{
			var type = Reflector.Wrap(typeof(ClassWithDefinedCollection));
			var framework = TestableXunitTestFrameworkDiscoverer.Create(type.Assembly);

			var testClass = framework.CreateTestClass(type);

			Assert.NotNull(testClass.TestCollection);
			Assert.Equal("This a defined collection", testClass.TestCollection.DisplayName);
			Assert.NotNull(testClass.TestCollection.CollectionDefinition);
			Assert.Equal("XunitTestFrameworkDiscovererTests+CreateTestClass+DeclaredCollection", testClass.TestCollection.CollectionDefinition.Name);
		}
	}

	class ClassWithSingleTest
	{
		[Fact]
		public static void TestMethod() { }
	}

	abstract class AbstractClass
	{
		[Fact]
		public static void ATestNotToBeRun() { }
	}

	public class ReportDiscoveredTestCase
	{
		TestableXunitTestFrameworkDiscoverer framework;
		SpyMessageBus messageBus;

		public ReportDiscoveredTestCase()
		{
			messageBus = new SpyMessageBus();

			var sourceProvider = Substitute.For<_ISourceInformationProvider>();
			sourceProvider
				.GetSourceInformation(null, null)
				.ReturnsForAnyArgs(new _SourceInformation { FileName = "Source File", LineNumber = 42 });

			framework = TestableXunitTestFrameworkDiscoverer.Create(sourceProvider: sourceProvider);
		}

		[Fact]
		public void CallsSourceProviderWhenTestCaseSourceInformationIsMissing()
		{
			var testCase = Mocks.TestCase<ClassWithSingleTest>(nameof(ClassWithSingleTest.TestMethod));

			framework.ReportDiscoveredTestCase_Public(testCase, includeSerialization: false, includeSourceInformation: true, messageBus);

			var msg = Assert.Single(messageBus.Messages);
			var discoveryMsg = Assert.IsAssignableFrom<_TestCaseDiscovered>(msg);
			Assert.Same(testCase, discoveryMsg.TestCase);
			Assert.Equal("Source File", testCase.SourceInformation?.FileName);
			Assert.Equal(42, testCase.SourceInformation?.LineNumber);
		}

		[Fact]
		public void DoesNotCallSourceProviderWhenTestCaseSourceInformationIsPresent()
		{
			var testCase = Mocks.TestCase<ClassWithSingleTest>(nameof(ClassWithSingleTest.TestMethod), fileName: "Alt Source File", lineNumber: 2112);

			framework.ReportDiscoveredTestCase_Public(testCase, includeSerialization: false, includeSourceInformation: true, messageBus);

			var msg = Assert.Single(messageBus.Messages);
			var discoveryMsg = Assert.IsAssignableFrom<_TestCaseDiscovered>(msg);
			Assert.Same(testCase, discoveryMsg.TestCase);
			Assert.Equal("Alt Source File", testCase.SourceInformation?.FileName);
			Assert.Equal(2112, testCase.SourceInformation?.LineNumber);
		}

		[Theory]
		[InlineData(false, null)]
		[InlineData(true, ":F:XunitTestFrameworkDiscovererTests+ClassWithSingleTest:TestMethod:1:0")]
		public void SerializationTestsForXunitTestCase(
			bool includeSerialization,
			string? expectedSerializationStartingText)
		{
			var messageSink = SpyMessageSink.Create();
			var testMethod = Mocks.TestMethod<ClassWithSingleTest>(nameof(ClassWithSingleTest.TestMethod));
			var testCase = new XunitTestCase(messageSink, TestMethodDisplay.ClassAndMethod, TestMethodDisplayOptions.None, testMethod);

			framework.ReportDiscoveredTestCase_Public(testCase, includeSerialization, includeSourceInformation: true, messageBus);

			var msg = Assert.Single(messageBus.Messages);
			var discoveryMsg = Assert.IsAssignableFrom<_TestCaseDiscovered>(msg);
			if (expectedSerializationStartingText != null)
				Assert.Equal(expectedSerializationStartingText, discoveryMsg.Serialization);
			else
				Assert.Null(discoveryMsg.Serialization);
		}
	}

	public class TestableXunitTestFrameworkDiscoverer : XunitTestFrameworkDiscoverer
	{
		protected TestableXunitTestFrameworkDiscoverer()
			: this(Mocks.AssemblyInfo()) { }

		protected TestableXunitTestFrameworkDiscoverer(IAssemblyInfo assembly)
			: this(assembly, null, null, null) { }

		protected TestableXunitTestFrameworkDiscoverer(
			IAssemblyInfo assembly,
			_ISourceInformationProvider? sourceProvider,
			_IMessageSink? diagnosticMessageSink,
			IXunitTestCollectionFactory? collectionFactory)
				: base(assembly, configFileName: null, sourceProvider ?? Substitute.For<_ISourceInformationProvider>(), diagnosticMessageSink ?? new _NullMessageSink(), collectionFactory)
		{
			Assembly = assembly;
			Sink = new TestableTestDiscoverySink();
		}

		public IAssemblyInfo Assembly { get; private set; }

		public override sealed string TestAssemblyUniqueID => "asm-id";

		public List<_ITestCase> TestCases
		{
			get
			{
				Sink.Finished.WaitOne();
				return Sink.TestCases;
			}
		}

		internal TestableTestDiscoverySink Sink { get; private set; }

		public static TestableXunitTestFrameworkDiscoverer Create(
			IAssemblyInfo? assembly = null,
			_ISourceInformationProvider? sourceProvider = null,
			_IMessageSink? diagnosticMessageSink = null,
			IXunitTestCollectionFactory? collectionFactory = null)
		{
			return new TestableXunitTestFrameworkDiscoverer(assembly ?? Mocks.AssemblyInfo(), sourceProvider, diagnosticMessageSink, collectionFactory);
		}

		public new _ITestClass CreateTestClass(ITypeInfo @class)
		{
			return base.CreateTestClass(@class);
		}

		public void Find()
		{
			base.Find(Sink, _TestFrameworkOptions.ForDiscovery());
			Sink.Finished.WaitOne();
		}

		public void Find(string typeName)
		{
			Find(typeName, Sink, _TestFrameworkOptions.ForDiscovery());
			Sink.Finished.WaitOne();
		}

		public virtual bool FindTestsForClass(_ITestClass testClass)
		{
			using var messageBus = new MessageBus(Sink);
			return base.FindTestsForType(testClass, messageBus, _TestFrameworkOptions.ForDiscovery());
		}

		protected sealed override bool FindTestsForType(
			_ITestClass testClass,
			IMessageBus messageBus,
			_ITestFrameworkDiscoveryOptions discoveryOptions)
		{
			return FindTestsForClass(testClass);
		}

		protected sealed override bool IsValidTestClass(ITypeInfo type)
		{
			return base.IsValidTestClass(type);
		}

		public bool ReportDiscoveredTestCase_Public(
			_ITestCase testCase,
			bool includeSerialization,
			bool includeSourceInformation,
			IMessageBus messageBus) =>
				ReportDiscoveredTestCase(testCase, includeSerialization, includeSourceInformation, messageBus);
	}

	internal class TestableTestDiscoverySink : TestDiscoverySink
	{
		public bool StartSeen = false;

		public TestableTestDiscoverySink(Func<bool>? cancelThunk = null)
			: base(cancelThunk)
		{
			DiscoverySink.DiscoveryStartingEvent += args => StartSeen = true;
		}
	}
}
