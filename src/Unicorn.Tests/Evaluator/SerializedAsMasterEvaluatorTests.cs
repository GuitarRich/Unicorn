﻿using System;
using FluentAssertions;
using NSubstitute;
using Xunit;
using Rainbow.Diff;
using Rainbow.Filtering;
using Rainbow.Model;
using Rainbow.Storage;
using Rainbow.Storage.Sc;
using Rainbow.Storage.Sc.Deserialization;
using Rainbow.Tests;
using Sitecore.Data.Items;
using Sitecore.FakeDb;
using Unicorn.Configuration;
using Unicorn.Data;
using Unicorn.Evaluators;
using Unicorn.Logging;

namespace Unicorn.Tests.Evaluator
{
	public class SerializedAsMasterEvaluatorTests
	{
		[Fact]
		public void EvaluateOrphans_ThrowsArgumentNullException_WhenItemsAreNull()
		{
			var evaluator = CreateTestEvaluator();

			Assert.Throws<ArgumentNullException>(() => evaluator.EvaluateOrphans(null));
		}

		[Theory, AutoDbData]
		public void EvaluateOrphans_RecyclesOrphanItem(Db db, Item item)
		{
			var evaluator = CreateTestEvaluator();

			evaluator.EvaluateOrphans(new IItemData[] { new ItemData(item) });

			db.GetItem(item.ID).Should().BeNull();
		}

		[Fact]
		public void EvaluateNewSerializedItem_ThrowsArgumentNullException_WhenNewItemIsNull()
		{
			var evaluator = CreateTestEvaluator();

			Assert.Throws<ArgumentNullException>(() => evaluator.EvaluateNewSerializedItem(null));
		}

		[Theory, AutoDbData]
		public void EvaluateNewSerializedItem_LogsCreatedItem(Db db, DbItem dbItem)
		{
			db.Add(dbItem);
			var item = db.GetItem(dbItem.ID);

			var logger = Substitute.For<ISerializedAsMasterEvaluatorLogger>();
			var evaluator = CreateTestEvaluator(logger);
			var itemData = new ItemData(item);

			evaluator.EvaluateNewSerializedItem(itemData);

			logger.Received().DeserializedNewItem(itemData);
		}

		[Theory, AutoDbData]
		public void EvaluateNewSerializedItem_DeserializesItem(Db db, DbItem dbItem)
		{
			db.Add(dbItem);
			var item = db.GetItem(dbItem.ID);

			var deserializer = Substitute.For<IDeserializer>();
			var evaluator = CreateTestEvaluator(deserializer: deserializer);
			var itemData = new ItemData(item);

			evaluator.EvaluateNewSerializedItem(itemData);

			deserializer.Received().Deserialize(itemData);
		}

		[Fact]
		public void EvaluateUpdate_ThrowsArgumentNullException_WhenSerializedItemIsNull()
		{
			var evaluator = CreateTestEvaluator();

			Assert.Throws<ArgumentNullException>(() => evaluator.EvaluateUpdate(null, new FakeItem()));
		}

		[Fact]
		public void EvaluateUpdate_ThrowsArgumentNullException_WhenExistingItemIsNull()
		{
			var evaluator = CreateTestEvaluator();

			Assert.Throws<ArgumentNullException>(() => evaluator.EvaluateUpdate(new FakeItem(), null));
		}

		[Fact]
		public void EvaluateUpdate_Deserializes_ComparerReturnsNotEqual()
		{
			var comparer = Substitute.For<IItemComparer>();
			comparer.FastCompare(Arg.Any<IItemData>(), Arg.Any<IItemData>()).Returns(new ItemComparisonResult(new FakeItem(), new FakeItem(), true));

			var deserializer = Substitute.For<IDeserializer>();

			var evaluator = CreateTestEvaluator(deserializer: deserializer, comparer: comparer);

			evaluator.EvaluateUpdate(new FakeItem(), new FakeItem());

			deserializer.Received().Deserialize(Arg.Any<IItemData>());
		}

		[Fact]
		public void EvaluateUpdate_DoesNotDeserialize_ComparerReturnsEqual()
		{
			var comparer = Substitute.For<IItemComparer>();
			comparer.FastCompare(Arg.Any<IItemData>(), Arg.Any<IItemData>()).Returns(new ItemComparisonResult(new FakeItem(), new FakeItem()));

			var deserializer = Substitute.For<IDeserializer>();

			var evaluator = CreateTestEvaluator(deserializer: deserializer, comparer: comparer);

			evaluator.EvaluateUpdate(new FakeItem(), new FakeItem());

			deserializer.DidNotReceive().Deserialize(Arg.Any<IItemData>());
		}

		private SerializedAsMasterEvaluator CreateTestEvaluator(ISerializedAsMasterEvaluatorLogger logger = null, IDeserializer deserializer = null, IItemComparer comparer = null)
		{
			if (deserializer == null) deserializer = Substitute.For<IDeserializer>();

			var dataStore = new ConfigurationDataStore(new Lazy<IDataStore>(() => new SitecoreDataStore(deserializer)));

			if (comparer == null)
			{
				comparer = Substitute.For<IItemComparer>();
				comparer.FastCompare(Arg.Any<IItemData>(), Arg.Any<IItemData>()).Returns(new ItemComparisonResult(new FakeItem(), new FakeItem()));
			}

			return new SerializedAsMasterEvaluator(Substitute.For<ILogger>(), logger ?? Substitute.For<ISerializedAsMasterEvaluatorLogger>(), comparer, CreateTestFieldFilter(), dataStore, Substitute.For<ITargetDataStore>(), Substitute.For<IConfiguration>());
		}

		private IFieldFilter CreateTestFieldFilter()
		{
			var trueFilter = Substitute.For<IFieldFilter>();
			trueFilter.Includes(Arg.Any<Guid>()).Returns(true);

			return trueFilter;
		}
	}
}
