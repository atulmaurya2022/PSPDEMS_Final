using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EMS.Tests
{
    public class BatchScenarioTests
    {
        private ApplicationDbContext CreateInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new ApplicationDbContext(options, new HttpContextAccessor());
        }

        [Fact]
        public async Task Scenario1_MultipleReceiptsOnDifferentDates_AggregatesAndDeductsFIFO()
        {
            var dbName = Guid.NewGuid().ToString();
            using var db = CreateInMemoryDbContext(dbName);
            var repo = new CompounderIndentRepository(db);

            int medItemId = 101;
            int plantId = 1;

            // Setup Store Indent 1 (18/09/2026) - Batch B001, Qty 10
            var storeIndent1 = new StoreIndent { IndentId = 1, PlantId = (short)plantId, Status = "Approved", IndentType = "Regular", CreatedBy = "StoreUser" };
            var storeItem1 = new StoreIndentItem { IndentItemId = 1, IndentId = 1, MedItemId = medItemId, RaisedQuantity = 10, ReceivedQuantity = 10 };
            var storeBatch1 = new StoreIndentBatch
            {
                BatchId = 1,
                IndentItemId = 1,
                BatchNo = "B001",
                VendorCode = "V001",
                ExpiryDate = DateTime.Today.AddDays(180),
                ReceivedQuantity = 10,
                AvailableStock = 10
            };

            // Setup Store Indent 2 (29/09/2026) - Batch B001, Qty 20
            var storeIndent2 = new StoreIndent { IndentId = 2, PlantId = (short)plantId, Status = "Approved", IndentType = "Regular", CreatedBy = "StoreUser" };
            var storeItem2 = new StoreIndentItem { IndentItemId = 2, IndentId = 2, MedItemId = medItemId, RaisedQuantity = 20, ReceivedQuantity = 20 };
            var storeBatch2 = new StoreIndentBatch
            {
                BatchId = 2,
                IndentItemId = 2,
                BatchNo = "B001",
                VendorCode = "V001",
                ExpiryDate = DateTime.Today.AddDays(200),
                ReceivedQuantity = 20,
                AvailableStock = 20
            };

            db.StoreIndents.AddRange(storeIndent1, storeIndent2);
            db.StoreIndentItems.AddRange(storeItem1, storeItem2);
            db.StoreIndentBatches.AddRange(storeBatch1, storeBatch2);
            await db.SaveChangesAsync();

            // 1. Verify available batches aggregation
            var availableBatches = (await repo.GetAvailableStoreBatchesForMedicineAsync(medItemId, plantId)).ToList();
            Assert.Single(availableBatches);
            Assert.Equal("B001", availableBatches[0].BatchNo);
            Assert.Equal(30, availableBatches[0].AvailableStock); // 10 + 20 = 30
            Assert.Equal(storeBatch1.ExpiryDate, availableBatches[0].ExpiryDate); // Earliest expiry

            // 2. Test GetStoreBatchesByBatchNoAndMedicineAsync returns both in FIFO order
            var storeBatches = await repo.GetStoreBatchesByBatchNoAndMedicineAsync("B001", medItemId, plantId);
            Assert.Equal(2, storeBatches.Count);
            Assert.Equal(1, storeBatches[0].BatchId);
            Assert.Equal(2, storeBatches[1].BatchId);

            // 3. Simulate issuing 15 units (FIFO deduction: 10 from Batch 1, 5 from Batch 2)
            int netIssued = 15;
            var remainingToDeduct = netIssued;
            foreach (var sb in storeBatches.Where(b => b.AvailableStock > 0).OrderBy(b => b.ExpiryDate).ThenBy(b => b.BatchId))
            {
                if (remainingToDeduct <= 0) break;
                var deduct = Math.Min(sb.AvailableStock, remainingToDeduct);
                sb.AvailableStock -= deduct;
                remainingToDeduct -= deduct;
            }
            await db.SaveChangesAsync();

            Assert.Equal(0, storeBatch1.AvailableStock); // 10 - 10 = 0
            Assert.Equal(15, storeBatch2.AvailableStock); // 20 - 5 = 15

            // 4. Verify updated available stock in repository
            var updatedBatches = (await repo.GetAvailableStoreBatchesForMedicineAsync(medItemId, plantId)).ToList();
            Assert.Single(updatedBatches);
            Assert.Equal(15, updatedBatches[0].AvailableStock);

            // 5. Verify GetStoreBatchByBatchNoAndMedicineAsync now returns Batch 2 (which still has stock)
            var activeBatch = await repo.GetStoreBatchByBatchNoAndMedicineAsync("B001", medItemId, plantId);
            Assert.NotNull(activeBatch);
            Assert.Equal(2, activeBatch.BatchId);
            Assert.Equal(15, activeBatch.AvailableStock);
        }

        [Fact]
        public async Task Scenario2_OlderReceiptExhausted_NewerReceiptActive_DoesNotShowZero()
        {
            var dbName = Guid.NewGuid().ToString();
            using var db = CreateInMemoryDbContext(dbName);
            var repo = new CompounderIndentRepository(db);

            int medItemId = 202;
            int plantId = 1;

            // Batch B002 Receipt 1: Fully consumed (AvailableStock = 0)
            var si1 = new StoreIndent { IndentId = 10, PlantId = (short)plantId, Status = "Approved", IndentType = "Regular", CreatedBy = "StoreUser" };
            var sii1 = new StoreIndentItem { IndentItemId = 10, IndentId = 10, MedItemId = medItemId, RaisedQuantity = 10, ReceivedQuantity = 10 };
            var sib1 = new StoreIndentBatch
            {
                BatchId = 10,
                IndentItemId = 10,
                BatchNo = "B002",
                VendorCode = "V001",
                ExpiryDate = DateTime.Today.AddDays(100),
                ReceivedQuantity = 10,
                AvailableStock = 0 // Exhausted!
            };

            // Batch B002 Receipt 2: Active (AvailableStock = 25)
            var si2 = new StoreIndent { IndentId = 11, PlantId = (short)plantId, Status = "Approved", IndentType = "Regular", CreatedBy = "StoreUser" };
            var sii2 = new StoreIndentItem { IndentItemId = 11, IndentId = 11, MedItemId = medItemId, RaisedQuantity = 25, ReceivedQuantity = 25 };
            var sib2 = new StoreIndentBatch
            {
                BatchId = 11,
                IndentItemId = 11,
                BatchNo = "B002",
                VendorCode = "V001",
                ExpiryDate = DateTime.Today.AddDays(200),
                ReceivedQuantity = 25,
                AvailableStock = 25 // Active!
            };

            db.StoreIndents.AddRange(si1, si2);
            db.StoreIndentItems.AddRange(sii1, sii2);
            db.StoreIndentBatches.AddRange(sib1, sib2);
            await db.SaveChangesAsync();

            // Verify available batches returns 25 (not 0)
            var availableBatches = (await repo.GetAvailableStoreBatchesForMedicineAsync(medItemId, plantId)).ToList();
            Assert.Single(availableBatches);
            Assert.Equal("B002", availableBatches[0].BatchNo);
            Assert.Equal(25, availableBatches[0].AvailableStock);

            // Verify GetStoreBatchByBatchNoAndMedicineAsync returns the active batch (BatchId 11)
            var activeBatch = await repo.GetStoreBatchByBatchNoAndMedicineAsync("B002", medItemId, plantId);
            Assert.NotNull(activeBatch);
            Assert.Equal(11, activeBatch.BatchId);
            Assert.Equal(25, activeBatch.AvailableStock);
        }

        [Fact]
        public async Task Scenario3_DifferentExpiryDates_SortsByEarliestExpiry()
        {
            var dbName = Guid.NewGuid().ToString();
            using var db = CreateInMemoryDbContext(dbName);
            var repo = new CompounderIndentRepository(db);

            int medItemId = 303;
            int plantId = 1;

            var si1 = new StoreIndent { IndentId = 20, PlantId = (short)plantId, Status = "Approved", IndentType = "Regular", CreatedBy = "StoreUser" };
            var sii1 = new StoreIndentItem { IndentItemId = 20, IndentId = 20, MedItemId = medItemId, RaisedQuantity = 10, ReceivedQuantity = 10 };
            var sib1 = new StoreIndentBatch
            {
                BatchId = 20,
                IndentItemId = 20,
                BatchNo = "B003",
                VendorCode = "V001",
                ExpiryDate = DateTime.Today.AddDays(300), // Later expiry
                ReceivedQuantity = 10,
                AvailableStock = 10
            };

            var si2 = new StoreIndent { IndentId = 21, PlantId = (short)plantId, Status = "Approved", IndentType = "Regular", CreatedBy = "StoreUser" };
            var sii2 = new StoreIndentItem { IndentItemId = 21, IndentId = 21, MedItemId = medItemId, RaisedQuantity = 15, ReceivedQuantity = 15 };
            var sib2 = new StoreIndentBatch
            {
                BatchId = 21,
                IndentItemId = 21,
                BatchNo = "B003",
                VendorCode = "V001",
                ExpiryDate = DateTime.Today.AddDays(60), // Earlier expiry!
                ReceivedQuantity = 15,
                AvailableStock = 15
            };

            db.StoreIndents.AddRange(si1, si2);
            db.StoreIndentItems.AddRange(sii1, sii2);
            db.StoreIndentBatches.AddRange(sib1, sib2);
            await db.SaveChangesAsync();

            // Available batches should pick earliest expiry date (60 days)
            var availableBatches = (await repo.GetAvailableStoreBatchesForMedicineAsync(medItemId, plantId)).ToList();
            Assert.Single(availableBatches);
            Assert.Equal(sib2.ExpiryDate, availableBatches[0].ExpiryDate);
            Assert.Equal(25, availableBatches[0].AvailableStock);
            Assert.True(availableBatches[0].IsEarliestExpiry);

            // FIFO order should put earlier expiring batch (BatchId 21) first
            var storeBatches = await repo.GetStoreBatchesByBatchNoAndMedicineAsync("B003", medItemId, plantId);
            Assert.Equal(2, storeBatches.Count);
            Assert.Equal(21, storeBatches[0].BatchId); // Earlier expiry first
            Assert.Equal(20, storeBatches[1].BatchId);
        }

        [Fact]
        public async Task Scenario4_PlantWiseIsolation_RespectsPlantId()
        {
            var dbName = Guid.NewGuid().ToString();
            using var db = CreateInMemoryDbContext(dbName);
            var repo = new CompounderIndentRepository(db);

            int medItemId = 404;

            // Plant 1 batch
            var si1 = new StoreIndent { IndentId = 30, PlantId = 1, Status = "Approved", IndentType = "Regular", CreatedBy = "StoreUser" };
            var sii1 = new StoreIndentItem { IndentItemId = 30, IndentId = 30, MedItemId = medItemId, RaisedQuantity = 10, ReceivedQuantity = 10 };
            var sib1 = new StoreIndentBatch { BatchId = 30, IndentItemId = 30, BatchNo = "B004", VendorCode = "V001", ExpiryDate = DateTime.Today.AddDays(100), ReceivedQuantity = 10, AvailableStock = 10 };

            // Plant 2 batch
            var si2 = new StoreIndent { IndentId = 31, PlantId = 2, Status = "Approved", IndentType = "Regular", CreatedBy = "StoreUser" };
            var sii2 = new StoreIndentItem { IndentItemId = 31, IndentId = 31, MedItemId = medItemId, RaisedQuantity = 25, ReceivedQuantity = 25 };
            var sib2 = new StoreIndentBatch { BatchId = 31, IndentItemId = 31, BatchNo = "B004", VendorCode = "V001", ExpiryDate = DateTime.Today.AddDays(100), ReceivedQuantity = 25, AvailableStock = 25 };

            db.StoreIndents.AddRange(si1, si2);
            db.StoreIndentItems.AddRange(sii1, sii2);
            db.StoreIndentBatches.AddRange(sib1, sib2);
            await db.SaveChangesAsync();

            // Query Plant 1
            var plant1Batches = (await repo.GetAvailableStoreBatchesForMedicineAsync(medItemId, 1)).ToList();
            Assert.Single(plant1Batches);
            Assert.Equal(10, plant1Batches[0].AvailableStock);

            // Query Plant 2
            var plant2Batches = (await repo.GetAvailableStoreBatchesForMedicineAsync(medItemId, 2)).ToList();
            Assert.Single(plant2Batches);
            Assert.Equal(25, plant2Batches[0].AvailableStock);
        }

        [Fact]
        public async Task Scenario5_DeleteCompounderBatch_RestoresStoreStockAcrossReceipts()
        {
            var dbName = Guid.NewGuid().ToString();
            using var db = CreateInMemoryDbContext(dbName);
            var repo = new CompounderIndentRepository(db);

            int medItemId = 505;
            short plantId = 1;

            // Store Indent with 2 receipts: Receipt 1 has 0 available (was 10), Receipt 2 has 15 available (was 20)
            var storeIndent = new StoreIndent { IndentId = 50, PlantId = plantId, Status = "Approved", IndentType = "Regular", CreatedBy = "StoreUser" };
            var storeItem = new StoreIndentItem { IndentItemId = 50, IndentId = 50, MedItemId = medItemId, RaisedQuantity = 30, ReceivedQuantity = 30 };
            var storeBatch1 = new StoreIndentBatch { BatchId = 50, IndentItemId = 50, BatchNo = "B005", VendorCode = "V001", ExpiryDate = DateTime.Today.AddDays(100), ReceivedQuantity = 10, AvailableStock = 0 };
            var storeBatch2 = new StoreIndentBatch { BatchId = 51, IndentItemId = 50, BatchNo = "B005", VendorCode = "V001", ExpiryDate = DateTime.Today.AddDays(200), ReceivedQuantity = 20, AvailableStock = 15 };

            // Compounder Indent having an issued batch with AvailableStock = 15
            var compIndent = new CompounderIndent { IndentId = 100, plant_id = plantId, Status = "Approved", IndentType = "Regular", CreatedBy = "CompUser" };
            var compItem = new CompounderIndentItem { IndentItemId = 100, IndentId = 100, MedItemId = medItemId, RaisedQuantity = 15, ReceivedQuantity = 15 };
            var compBatch = new CompounderIndentBatch { BatchId = 100, IndentItemId = 100, BatchNo = "B005", VendorCode = "V001", ExpiryDate = DateTime.Today.AddDays(100), ReceivedQuantity = 15, AvailableStock = 15 };

            db.StoreIndents.Add(storeIndent);
            db.StoreIndentItems.Add(storeItem);
            db.StoreIndentBatches.AddRange(storeBatch1, storeBatch2);

            db.CompounderIndents.Add(compIndent);
            db.CompounderIndentItems.Add(compItem);
            db.CompounderIndentBatches.Add(compBatch);

            await db.SaveChangesAsync();

            // Delete compounder batch
            await repo.DeleteBatchAsync(100, 100, plantId);

            // Verify compounder batch deleted
            var deletedCompBatch = await db.CompounderIndentBatches.FindAsync(100);
            Assert.Null(deletedCompBatch);

            // Verify store stock restored: Receipt 2 had 15/20 (space 5) -> becomes 20; Receipt 1 gets remaining 10 -> becomes 10
            Assert.Equal(20, storeBatch2.AvailableStock);
            Assert.Equal(10, storeBatch1.AvailableStock);
        }
    }
}
