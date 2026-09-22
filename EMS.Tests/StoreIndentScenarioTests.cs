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
    public class StoreIndentScenarioTests
    {
        private ApplicationDbContext CreateInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new ApplicationDbContext(options, new HttpContextAccessor());
        }

        [Fact]
        public async Task StoreIndent_MultipleReceiptsOnDifferentDates_CalculatesTotalAvailableStockCorrectly()
        {
            var dbName = Guid.NewGuid().ToString();
            using var db = CreateInMemoryDbContext(dbName);
            var repo = new StoreIndentRepository(db);

            int medItemId = 1001;
            int plantId = 1;

            // Store Indent 1 (Date: 18/09/2026) - Batch B001, Qty: 10, Available: 10
            var indent1 = new StoreIndent
            {
                IndentId = 1,
                PlantId = (short)plantId,
                IndentType = "Regular",
                IndentDate = DateTime.Today.AddDays(-10),
                Status = "Approved",
                CreatedBy = "StoreUser"
            };
            var item1 = new StoreIndentItem
            {
                IndentItemId = 1,
                IndentId = 1,
                MedItemId = medItemId,
                RaisedQuantity = 10,
                ReceivedQuantity = 10,
                UnitPrice = 50,
                TotalAmount = 500
            };
            var batch1 = new StoreIndentBatch
            {
                BatchId = 1,
                IndentItemId = 1,
                BatchNo = "B001",
                VendorCode = "V001",
                ExpiryDate = DateTime.Today.AddDays(180),
                ReceivedQuantity = 10,
                AvailableStock = 10
            };

            // Store Indent 2 (Date: 29/09/2026) - Same Batch B001, Qty: 20, Available: 20
            var indent2 = new StoreIndent
            {
                IndentId = 2,
                PlantId = (short)plantId,
                IndentType = "Regular",
                IndentDate = DateTime.Today,
                Status = "Approved",
                CreatedBy = "StoreUser"
            };
            var item2 = new StoreIndentItem
            {
                IndentItemId = 2,
                IndentId = 2,
                MedItemId = medItemId,
                RaisedQuantity = 20,
                ReceivedQuantity = 20,
                UnitPrice = 50,
                TotalAmount = 1000
            };
            var batch2 = new StoreIndentBatch
            {
                BatchId = 2,
                IndentItemId = 2,
                BatchNo = "B001",
                VendorCode = "V001",
                ExpiryDate = DateTime.Today.AddDays(180),
                ReceivedQuantity = 20,
                AvailableStock = 20
            };

            db.StoreIndents.AddRange(indent1, indent2);
            db.StoreIndentItems.AddRange(item1, item2);
            db.StoreIndentBatches.AddRange(batch1, batch2);
            await db.SaveChangesAsync();

            // Test 1: GetTotalAvailableStockFromStoreAsync with plantId
            var totalStockPlant1 = await repo.GetTotalAvailableStockFromStoreAsync(medItemId, plantId);
            Assert.Equal(30, totalStockPlant1); // 10 + 20 = 30

            // Test 2: GetTotalAvailableStockFromStoreAsync without plantId
            var totalStockGlobal = await repo.GetTotalAvailableStockFromStoreAsync(medItemId, null);
            Assert.Equal(30, totalStockGlobal);

            // Test 3: GetStoreBatchByBatchNoAndMedicineAsync returns batch with available stock > 0
            var foundBatch = await repo.GetStoreBatchByBatchNoAndMedicineAsync("B001", medItemId, plantId);
            Assert.NotNull(foundBatch);
            Assert.Equal("B001", foundBatch.BatchNo);
            Assert.True(foundBatch.AvailableStock > 0);

            // Test 4: GetBatchesByIndentItemIdAsync for each indent item
            var batchesItem1 = await repo.GetBatchesByIndentItemIdAsync(1, plantId);
            Assert.Single(batchesItem1);
            Assert.Equal(10, batchesItem1[0].ReceivedQuantity);
            Assert.Equal(10, batchesItem1[0].AvailableStock);

            var batchesItem2 = await repo.GetBatchesByIndentItemIdAsync(2, plantId);
            Assert.Single(batchesItem2);
            Assert.Equal(20, batchesItem2[0].ReceivedQuantity);
            Assert.Equal(20, batchesItem2[0].AvailableStock);
        }

        [Fact]
        public async Task StoreIndent_AddOrUpdateBatches_UpdatesItemAndBatchesCorrectly()
        {
            var dbName = Guid.NewGuid().ToString();
            using var db = CreateInMemoryDbContext(dbName);
            var repo = new StoreIndentRepository(db);

            int medItemId = 1002;
            int plantId = 1;

            var plant = new OrgPlant { plant_id = (short)plantId, plant_name = "Main Plant", plant_code = "MP01" };
            var medMaster = new MedMaster { MedItemId = medItemId, MedItemName = "Paracetamol 500mg", plant_id = (short)plantId };
            var indent = new StoreIndent { IndentId = 10, PlantId = (short)plantId, IndentType = "Regular", Status = "Approved", CreatedBy = "StoreUser", OrgPlant = plant };
            var item = new StoreIndentItem { IndentItemId = 10, IndentId = 10, MedItemId = medItemId, RaisedQuantity = 50, ReceivedQuantity = 0, UnitPrice = 100, MedMaster = medMaster, StoreIndent = indent };

            db.org_plants.Add(plant);
            db.med_masters.Add(medMaster);
            db.StoreIndents.Add(indent);
            db.StoreIndentItems.Add(item);
            await db.SaveChangesAsync();

            // Add 2 batches to the store indent item
            var newBatches = new List<StoreIndentBatch>
            {
                new StoreIndentBatch { IndentItemId = 10, BatchNo = "BATCH-A", VendorCode = "V001", ExpiryDate = DateTime.Today.AddDays(100), ReceivedQuantity = 20, AvailableStock = 20 },
                new StoreIndentBatch { IndentItemId = 10, BatchNo = "BATCH-B", VendorCode = "V001", ExpiryDate = DateTime.Today.AddDays(200), ReceivedQuantity = 30, AvailableStock = 30 }
            };

            await repo.AddOrUpdateBatchesAsync(10, newBatches, plantId);

            // Verify batches saved
            var savedBatches = await repo.GetBatchesByIndentItemIdAsync(10, plantId);
            Assert.Equal(2, savedBatches.Count);
            Assert.Equal(50, savedBatches.Sum(b => b.ReceivedQuantity));
            Assert.Equal(50, savedBatches.Sum(b => b.AvailableStock));

            // Verify indent item received quantity & total amount updated
            var updatedItem = await repo.GetItemByIdAsync(10, plantId);
            Assert.NotNull(updatedItem);
            Assert.Equal(50, updatedItem.ReceivedQuantity);
            Assert.Equal(5000m, updatedItem.TotalAmount); // 50 * 100
        }

        [Fact]
        public async Task StoreIndent_DeleteBatch_RemovesBatchAndUpdatesAvailableStock()
        {
            var dbName = Guid.NewGuid().ToString();
            using var db = CreateInMemoryDbContext(dbName);
            var repo = new StoreIndentRepository(db);

            int medItemId = 1003;
            int plantId = 1;

            var indent = new StoreIndent { IndentId = 20, PlantId = (short)plantId, IndentType = "Regular", Status = "Approved", CreatedBy = "StoreUser" };
            var item = new StoreIndentItem { IndentItemId = 20, IndentId = 20, MedItemId = medItemId, RaisedQuantity = 20, ReceivedQuantity = 20 };
            var batch = new StoreIndentBatch { BatchId = 100, IndentItemId = 20, BatchNo = "DEL-BATCH", VendorCode = "V001", ExpiryDate = DateTime.Today.AddDays(100), ReceivedQuantity = 20, AvailableStock = 20 };

            db.StoreIndents.Add(indent);
            db.StoreIndentItems.Add(item);
            db.StoreIndentBatches.Add(batch);
            await db.SaveChangesAsync();

            // Delete batch
            await repo.DeleteBatchAsync(100, plantId);

            // Verify deleted
            var batches = await repo.GetBatchesByIndentItemIdAsync(20, plantId);
            Assert.Empty(batches);

            // Verify available stock is now 0
            var availableStock = await repo.GetTotalAvailableStockFromStoreAsync(medItemId, plantId);
            Assert.Equal(0, availableStock);
        }

        [Fact]
        public async Task StoreIndent_StoreInventoryReport_ReportsAggregatedStockAndStatus()
        {
            var dbName = Guid.NewGuid().ToString();
            using var db = CreateInMemoryDbContext(dbName);
            var repo = new StoreIndentRepository(db);

            int medItemId = 1004;
            int plantId = 1;

            var medBase = new MedBase { BaseId = 1, BaseName = "500mg" };
            var medMaster = new MedMaster { MedItemId = medItemId, MedItemName = "Paracetamol 500mg", CompanyName = "PharmaCorp", BaseId = 1, MedBase = medBase, ReorderLimit = 10 };
            var plant = new OrgPlant { plant_id = (short)plantId, plant_name = "Main Plant", plant_code = "MP01" };

            var indent = new StoreIndent { IndentId = 30, PlantId = (short)plantId, IndentType = "Regular", IndentDate = DateTime.Today, Status = "Approved", CreatedBy = "StoreInCharge", OrgPlant = plant };
            var item = new StoreIndentItem { IndentItemId = 30, IndentId = 30, MedItemId = medItemId, RaisedQuantity = 50, ReceivedQuantity = 50, MedMaster = medMaster };
            var batch1 = new StoreIndentBatch { BatchId = 301, IndentItemId = 30, BatchNo = "B001", VendorCode = "V001", ExpiryDate = DateTime.Today.AddDays(150), ReceivedQuantity = 20, AvailableStock = 20 };
            var batch2 = new StoreIndentBatch { BatchId = 302, IndentItemId = 30, BatchNo = "B002", VendorCode = "V001", ExpiryDate = DateTime.Today.AddDays(250), ReceivedQuantity = 30, AvailableStock = 10 }; // 20 consumed

            db.med_bases.Add(medBase);
            db.med_masters.Add(medMaster);
            db.org_plants.Add(plant);
            db.StoreIndents.Add(indent);
            db.StoreIndentItems.Add(item);
            db.StoreIndentBatches.AddRange(batch1, batch2);
            await db.SaveChangesAsync();

            var report = (await repo.GetStoreInventoryBatchReportAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1), plantId)).ToList();

            Assert.Equal(2, report.Count);

            var r1 = report.FirstOrDefault(r => r.BatchNo == "B001");
            Assert.NotNull(r1);
            Assert.Equal(20, r1.ReceivedQuantity);
            Assert.Equal(20, r1.AvailableStock);
            Assert.Equal(0, r1.ConsumedStock);
            Assert.Equal("In Stock", r1.StockStatus);

            var r2 = report.FirstOrDefault(r => r.BatchNo == "B002");
            Assert.NotNull(r2);
            Assert.Equal(30, r2.ReceivedQuantity);
            Assert.Equal(10, r2.AvailableStock);
            Assert.Equal(20, r2.ConsumedStock); // 30 - 10 = 20
        }
    }
}
