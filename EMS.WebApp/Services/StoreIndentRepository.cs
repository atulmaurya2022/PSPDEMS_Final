using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml.FormulaParsing.ExpressionGraph;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public class StoreIndentRepository : IStoreIndentRepository
    {
        private readonly ApplicationDbContext _db;

        public StoreIndentRepository(ApplicationDbContext db) => _db = db;

        public async Task<IEnumerable<StoreIndent>> ListAsync(string currentUser = null, int? userPlantId = null)
        {
            var query = _db.StoreIndents.AsQueryable();

            // Plant-wise filtering for non-admin users
            if (userPlantId.HasValue)
            {
                query = query.Where(s => s.PlantId == userPlantId.Value);
            }

            // Filter drafts to show only to their creators
            if (!string.IsNullOrEmpty(currentUser))
            {
                query = query.Where(s => s.IndentType != "Draft Indent" || s.CreatedBy == currentUser);
            }
            else
            {
                // If no current user, exclude all drafts
                query = query.Where(s => s.IndentType != "Draft Indent");
            }

            return await query
                .Include(s => s.OrgPlant)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<StoreIndent>> ListByTypeAsync(string indentType, string currentUser = null, int? userPlantId = null)
        {
            var query = _db.StoreIndents.Where(s => s.IndentType == indentType);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(s => s.PlantId == userPlantId.Value);
            }

            // Additional filtering for Draft Indent - only show to creator
            if (indentType == "Draft Indent" && !string.IsNullOrEmpty(currentUser))
            {
                query = query.Where(s => s.CreatedBy == currentUser);
            }
            else if (indentType == "Draft Indent" && string.IsNullOrEmpty(currentUser))
            {
                // If no current user and requesting drafts, return empty
                return new List<StoreIndent>();
            }

            return await query
                .Include(s => s.OrgPlant)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<StoreIndent>> ListByStatusAsync(string status, string currentUser = null, int? userPlantId = null)
        {
            var query = _db.StoreIndents.Where(s => s.Status == status);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(s => s.PlantId == userPlantId.Value);
            }

            return await query
                .Include(s => s.OrgPlant)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();
        }

        public async Task<StoreIndent?> GetByIdAsync(int id, int? userPlantId = null)
        {
            var query = _db.StoreIndents.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(s => s.PlantId == userPlantId.Value);
            }

            return await query
                .Include(s => s.OrgPlant)
                .FirstOrDefaultAsync(s => s.IndentId == id);
        }

        public async Task<StoreIndent?> GetByIdWithItemsAsync(int id, int? userPlantId = null)
        {
            var query = _db.StoreIndents.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(s => s.PlantId == userPlantId.Value);
            }

            return await query
                .Include(s => s.StoreIndentItems)
                    .ThenInclude(i => i.MedMaster)
                .Include(s => s.OrgPlant)
                .FirstOrDefaultAsync(s => s.IndentId == id);
        }

        public async Task AddAsync(StoreIndent entity)
        {
            _db.StoreIndents.Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(StoreIndent entity)
        {
            _db.StoreIndents.Update(entity);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id, int? userPlantId = null)
        {
            var query = _db.StoreIndents.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(s => s.PlantId == userPlantId.Value);
            }

            var entity = await query
                .Include(s => s.StoreIndentItems)
                .FirstOrDefaultAsync(s => s.IndentId == id);

            if (entity != null)
            {
                _db.StoreIndents.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<MedMaster>> GetMedicinesAsync(int? userPlantId = null)
        {
            IQueryable<MedMaster> query = _db.med_masters;

            // FIXED: Add plant filtering for medicine master data
            if (userPlantId.HasValue)
            {
                query = query.Where(m => m.plant_id == userPlantId.Value);
            }

            return await query
                .Include(m => m.MedBase)
                .OrderBy(m => m.MedItemName)
                .ToListAsync();
        }

        public async Task<MedMaster?> GetMedicineByIdAsync(int medItemId, int? userPlantId = null)
        {
            IQueryable<MedMaster> query = _db.med_masters;

            // FIXED: Add plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(m => m.plant_id == userPlantId.Value);
            }

            return await query
                .Include(m => m.MedBase)
                .FirstOrDefaultAsync(m => m.MedItemId == medItemId);
        }
        // StoreIndentItem methods with plant filtering
        public async Task AddItemAsync(StoreIndentItem item)
        {
            _db.StoreIndentItems.Add(item);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateItemAsync(StoreIndentItem item)
        {
            _db.StoreIndentItems.Update(item);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteItemAsync(int indentItemId, int? userPlantId = null)
        {
            var query = from item in _db.StoreIndentItems
                        join indent in _db.StoreIndents on item.IndentId equals indent.IndentId
                        where item.IndentItemId == indentItemId
                        select item;

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = from item in _db.StoreIndentItems
                        join indent in _db.StoreIndents on item.IndentId equals indent.IndentId
                        where item.IndentItemId == indentItemId && indent.PlantId == userPlantId.Value
                        select item;
            }

            var itemToDelete = await query.FirstOrDefaultAsync();
            if (itemToDelete != null)
            {
                _db.StoreIndentItems.Remove(itemToDelete);
                await _db.SaveChangesAsync();
            }
        }

        public async Task<StoreIndentItem?> GetItemByIdAsync(int indentItemId, int? userPlantId = null)
        {
            var query = from item in _db.StoreIndentItems
                        join indent in _db.StoreIndents on item.IndentId equals indent.IndentId
                        where item.IndentItemId == indentItemId
                        select item;

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(item => _db.StoreIndents.Any(indent =>
                    indent.IndentId == item.IndentId && indent.PlantId == userPlantId.Value));
            }

            return await query
                .Include(i => i.MedMaster)
                .Include(i => i.StoreIndent)
                    .ThenInclude(s => s.OrgPlant)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<StoreIndentItem>> GetItemsByIndentIdAsync(int indentId, int? userPlantId = null)
        {
            var query = _db.StoreIndentItems.Where(i => i.IndentId == indentId);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(item => _db.StoreIndents.Any(indent =>
                    indent.IndentId == item.IndentId && indent.PlantId == userPlantId.Value));
            }

            return await query
                .Include(i => i.MedMaster)
                .Include(i => i.StoreIndent)
                    .ThenInclude(s => s.OrgPlant)
                .ToListAsync();
        }

        public async Task<bool> IsMedicineAlreadyAddedAsync(int indentId, int medItemId, int? excludeItemId = null, int? userPlantId = null)
        {
            var query = from item in _db.StoreIndentItems
                        join indent in _db.StoreIndents on item.IndentId equals indent.IndentId
                        where item.IndentId == indentId && item.MedItemId == medItemId
                        select item;

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(item => _db.StoreIndents.Any(indent =>
                    indent.IndentId == item.IndentId && indent.PlantId == userPlantId.Value));
            }

            if (excludeItemId.HasValue)
            {
                query = query.Where(i => i.IndentItemId != excludeItemId.Value);
            }

            return await query.AnyAsync();
        }

        // Batch-related methods with plant filtering
        public async Task<List<StoreIndentBatch>> GetBatchesByIndentItemIdAsync(int indentItemId, int? userPlantId = null)
        {
            var query = from batch in _db.StoreIndentBatches
                        join item in _db.StoreIndentItems on batch.IndentItemId equals item.IndentItemId
                        join indent in _db.StoreIndents on item.IndentId equals indent.IndentId
                        where batch.IndentItemId == indentItemId
                        select batch;

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(batch =>
                    _db.StoreIndentItems.Any(item =>
                        item.IndentItemId == batch.IndentItemId &&
                        _db.StoreIndents.Any(indent =>
                            indent.IndentId == item.IndentId && indent.PlantId == userPlantId.Value)));
            }

            return await query.ToListAsync();
        }

        public async Task AddOrUpdateBatchesAsync(int indentItemId, List<StoreIndentBatch> batches, int? userPlantId = null)
        {
            // Verify plant access before proceeding
            if (userPlantId.HasValue)
            {
                var hasAccess = await _db.StoreIndentItems.AnyAsync(item =>
                    item.IndentItemId == indentItemId &&
                    _db.StoreIndents.Any(indent =>
                        indent.IndentId == item.IndentId && indent.PlantId == userPlantId.Value));

                if (!hasAccess)
                {
                    throw new UnauthorizedAccessException("Access denied to this indent item.");
                }
            }



            // --- capture OLD store batches BEFORE delete ---
            var oldStoreBatches = await _db.StoreIndentBatches
                .Where(x => x.IndentItemId == indentItemId)
                .Select(x => new { x.BatchNo, x.ExpiryDate })
                .ToListAsync();





            // Delete all old batches for this item
            var old = await _db.StoreIndentBatches.Where(x => x.IndentItemId == indentItemId).ToListAsync();
            _db.StoreIndentBatches.RemoveRange(old);
            await _db.SaveChangesAsync();

            // Add new batches
            foreach (var batch in batches)
            {
                batch.BatchId = 0;
            }
            _db.StoreIndentBatches.AddRange(batches);
            await _db.SaveChangesAsync();

            // Update total received quantity
            var item = await _db.StoreIndentItems.FindAsync(indentItemId);
            if (item != null)
            {
                int receivedQuantity = batches.Sum(x => x.ReceivedQuantity);
                item.ReceivedQuantity = receivedQuantity; // batches.Sum(x => x.ReceivedQuantity);
                float priceCalc = 0;

                try
                {
                    if (item.UnitPrice.HasValue)
                    {
                        priceCalc = (float)item.UnitPrice * receivedQuantity;
                    }

                }
                catch { }
                item.TotalAmount = (decimal?)priceCalc;

                await _db.SaveChangesAsync();
            }



            // -----------------------
            // SIMPLE COMPOUNDER UPDATE
            // -----------------------

            // 1) Get med_item_id from store item
            var medItemId = await _db.StoreIndentItems
                .Where(x => x.IndentItemId == indentItemId)
                .Select(x => x.MedItemId)
                .FirstAsync();

            // 2) Get ALL compounder indent_item_ids for this med item
            //    (table: Compounder_Indent_Item)
            var compounderIndentItemIds = await _db.CompounderIndentItems
                .Where(ci => ci.MedItemId == medItemId)
                .Select(ci => ci.IndentItemId)
                .ToListAsync();

            if (compounderIndentItemIds.Count > 0 && oldStoreBatches.Count > 0)
            {
                // Build quick lookups for NEW batches
                var hasSingleNew = (batches.Count == 1);
                var singleNew = hasSingleNew ? batches[0] : null;

                // For multi-new, map by SAME BatchNo only
                var newByBatchNo = !hasSingleNew
                    ? batches.GroupBy(b => b.BatchNo).ToDictionary(g => g.Key, g => g.First())
                    : new Dictionary<string, StoreIndentBatch>();

                foreach (var oldBatch in oldStoreBatches)
                {
                    if (string.IsNullOrWhiteSpace(oldBatch.BatchNo)) continue;

                    StoreIndentBatch? targetNew = null;

                    if (hasSingleNew)
                    {
                        targetNew = singleNew!;
                    }
                    else
                    {
                        // Only replace if the same batch exists in the new list
                        if (newByBatchNo.TryGetValue(oldBatch.BatchNo!, out var match))
                            targetNew = match;
                    }

                    if (targetNew == null) continue; // skip if no simple mapping

                    // 3) Update in compounder_indent_batches
                    var rows = await _db.CompounderIndentBatches
                        .Where(cb =>
                            compounderIndentItemIds.Contains(cb.IndentItemId) &&
                            cb.BatchNo == oldBatch.BatchNo)
                        .ToListAsync();

                    if (rows.Count == 0) continue;

                    foreach (var r in rows)
                    {
                        r.BatchNo = targetNew.BatchNo;
                        r.ExpiryDate = targetNew.ExpiryDate;
                    }

                    await _db.SaveChangesAsync();
                }
            }



        }

        public async Task<StoreIndentBatch?> GetStoreBatchByBatchNoAndMedicineAsync(string batchNo, int medItemId, int? userPlantId = null)
        {
            var query = from sib in _db.StoreIndentBatches
                        join sii in _db.StoreIndentItems on sib.IndentItemId equals sii.IndentItemId
                        join si in _db.StoreIndents on sii.IndentId equals si.IndentId
                        where sib.BatchNo == batchNo
                              && sii.MedItemId == medItemId
                              && si.Status == "Approved"
                        select sib;

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(sib =>
                    _db.StoreIndentItems.Any(sii =>
                        sii.IndentItemId == sib.IndentItemId &&
                        _db.StoreIndents.Any(si =>
                            si.IndentId == sii.IndentId && si.PlantId == userPlantId.Value)));
            }

            return await query.FirstOrDefaultAsync();
        }

        public async Task UpdateBatchAvailableStockAsync(int batchId, int newAvailableStock, int? userPlantId = null)
        {
            var query = _db.StoreIndentBatches.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(batch =>
                    _db.StoreIndentItems.Any(item =>
                        item.IndentItemId == batch.IndentItemId &&
                        _db.StoreIndents.Any(indent =>
                            indent.IndentId == item.IndentId && indent.PlantId == userPlantId.Value)));
            }

            var batch = await query.FirstOrDefaultAsync(b => b.BatchId == batchId);
            if (batch != null)
            {
                batch.AvailableStock = newAvailableStock;
                await _db.SaveChangesAsync();
            }
        }

        public async Task DeleteBatchAsync(int batchId, int? userPlantId = null)
        {
            var query = _db.StoreIndentBatches.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(batch =>
                    _db.StoreIndentItems.Any(item =>
                        item.IndentItemId == batch.IndentItemId &&
                        _db.StoreIndents.Any(indent =>
                            indent.IndentId == item.IndentId && indent.PlantId == userPlantId.Value)));
            }

            var batch = await query.FirstOrDefaultAsync(b => b.BatchId == batchId);
            if (batch != null)
            {
                _db.StoreIndentBatches.Remove(batch);
                await _db.SaveChangesAsync();
            }
        }
        public async Task<int> GetTotalReceivedFromStoreAsync(int medItemId, int? userPlantId = null)
        {
            if (userPlantId.HasValue)
            {
                var query = from si in _db.StoreIndents
                            join sii in _db.StoreIndentItems on si.IndentId equals sii.IndentId
                            join sib in _db.StoreIndentBatches on sii.IndentItemId equals sib.IndentItemId
                            where sii.MedItemId == medItemId
                                  && si.Status == "Approved"
                                  && sib.ExpiryDate >= DateTime.Today
                                  && si.PlantId == userPlantId.Value  // FIXED: Direct plant filtering
                            //select sii.ReceivedQuantity;
                            select sib.AvailableStock;
                return await query.SumAsync();
            }
            else
            {
                var query = from si in _db.StoreIndents
                            join sii in _db.StoreIndentItems on si.IndentId equals sii.IndentId
                            join sib in _db.StoreIndentBatches on sii.IndentItemId equals sib.IndentItemId
                            where sii.MedItemId == medItemId && si.Status == "Approved"
                            && sib.ExpiryDate >= DateTime.Today
                            //select sii.ReceivedQuantity;
                            select sib.AvailableStock;
                return await query.SumAsync();
            }
        }

        //public async Task<int> GetTotalReceivedFromStoreAsync(int medItemId, int? userPlantId = null)
        //{
        //    var query = from item in _db.StoreIndentItems
        //                join indent in _db.StoreIndents on item.IndentId equals indent.IndentId
        //                //where item.MedItemId == medItemId
        //                where item.MedItemId == medItemId
        //                select item.ReceivedQuantity;

        //    // Plant-wise filtering
        //    if (userPlantId.HasValue)
        //    {
        //        query = from item in _db.StoreIndentItems
        //                join indent in _db.StoreIndents on item.IndentId equals indent.IndentId
        //                where item.MedItemId == medItemId && indent.PlantId == userPlantId.Value
        //                select item.ReceivedQuantity;
        //    }

        //    return await query.SumAsync();
        //}

        public async Task<int> GetTotalAvailableStockFromStoreAsync(int medItemId, int? userPlantId = null)
        {
            var query = from si in _db.StoreIndents
                        join sii in _db.StoreIndentItems on si.IndentId equals sii.IndentId
                        join sib in _db.StoreIndentBatches on sii.IndentItemId equals sib.IndentItemId
                        //where si.Status == "Approved" && sii.MedItemId == medItemId
                        where si.Status == "Approved" && sii.MedItemId == medItemId && sib.ExpiryDate >= DateTime.Today
                        select sib.AvailableStock;

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(stock =>
                    _db.StoreIndents.Any(si =>
                        _db.StoreIndentItems.Any(sii =>
                            _db.StoreIndentBatches.Any(sib =>
                                sib.AvailableStock == stock &&
                                sii.IndentItemId == sib.IndentItemId
                                && sib.ExpiryDate >= DateTime.Today
                                && si.IndentId == sii.IndentId &&
                                si.PlantId == userPlantId.Value))));
            }

            return await query.SumAsync();
        }

        // New helper methods for plant-based operations
        public async Task<int?> GetUserPlantIdAsync(string userName)
        {
            var user = await _db.SysUsers
                .FirstOrDefaultAsync(u => (u.adid == userName || u.email == userName || u.full_name == userName) && u.is_active);

            return user?.plant_id;
        }

        public async Task<bool> IsUserAuthorizedForIndentAsync(int indentId, int userPlantId)
        {
            return await _db.StoreIndents.AnyAsync(s => s.IndentId == indentId && s.PlantId == userPlantId);
        }

        // Report methods with plant filtering
        public async Task<IEnumerable<StoreIndentBatchReportDto>> GetStoreIndentBatchReportAsync(DateTime? fromDate = null, DateTime? toDate = null, int? userPlantId = null)
        {
            var query = _db.StoreIndents
                .Include(si => si.StoreIndentItems)
                .ThenInclude(sii => sii.MedMaster)
                .ThenInclude(mm => mm.MedBase)
                .Include(si => si.OrgPlant)
                .Where(si => si.Status != "Draft");

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(si => si.PlantId == userPlantId.Value);
            }

            // Apply date filtering if provided
            if (fromDate.HasValue)
            {
                query = query.Where(si => si.IndentDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(si => si.IndentDate <= toDate.Value);
            }

            var indents = await query.OrderBy(si => si.IndentDate).ToListAsync();

            var reportData = new List<StoreIndentBatchReportDto>();

            foreach (var indent in indents)
            {
                foreach (var item in indent.StoreIndentItems)
                {
                    // Get batches for this indent item
                    var batches = await _db.StoreIndentBatches
                        .Where(b => b.IndentItemId == item.IndentItemId)
                        .ToListAsync();

                    if (batches.Any())
                    {
                        foreach (var batch in batches)
                        {
                            reportData.Add(new StoreIndentBatchReportDto
                            {
                                IndentId = indent.IndentId,
                                IndentDate = indent.IndentDate,
                                MedicineName = item.MedMaster?.MedItemName ?? "Unknown Medicine",
                                Potency = item.MedMaster?.MedBase?.BaseName ?? "0",
                                ManufacturerName = item.MedMaster?.CompanyName ?? "Unknown Manufacturer",
                                BatchNo = batch.BatchNo ?? "Not Set",
                                VendorCode = batch.VendorCode ?? "Not Set",
                                RaisedQuantity = item.RaisedQuantity,
                                ReceivedQuantity = batch.ReceivedQuantity,
                                ExpiryDate = batch.ExpiryDate,
                                RaisedBy = indent.CreatedBy ?? "Unknown",
                                Status = indent.Status ?? "Unknown",
                                PlantName = indent.OrgPlant?.plant_name ?? "Unknown Plant" // NEW
                            });
                        }
                    }
                    else
                    {
                        reportData.Add(new StoreIndentBatchReportDto
                        {
                            IndentId = indent.IndentId,
                            IndentDate = indent.IndentDate,
                            MedicineName = item.MedMaster?.MedItemName ?? "Unknown Medicine",
                            Potency = item.MedMaster?.MedBase?.BaseName ?? "0",
                            ManufacturerName = item.MedMaster?.CompanyName ?? "Unknown Manufacturer",
                            BatchNo = "No Batch Info",
                            VendorCode = item.VendorCode ?? "Not Set",
                            RaisedQuantity = item.RaisedQuantity,
                            ReceivedQuantity = item.ReceivedQuantity,
                            ExpiryDate = item.ExpiryDate,
                            RaisedBy = indent.CreatedBy ?? "Unknown",
                            Status = indent.Status ?? "Unknown",
                            PlantName = indent.OrgPlant?.plant_name ?? "Unknown Plant" // NEW
                        });
                    }
                }
            }

            //return reportData.OrderBy(r => r.IndentDate).ThenBy(r => r.IndentId).ThenBy(r => r.BatchNo);
            return reportData.OrderBy(r => r.MedicineName).ThenBy(r => r.BatchNo);
        }

        public async Task<IEnumerable<StoreInventoryBatchReportDto>> GetStoreInventoryBatchReportAsync(
    DateTime? fromDate = null, DateTime? toDate = null, int? userPlantId = null)
        {
            var query = _db.StoreIndents
                .Include(si => si.StoreIndentItems)
                .ThenInclude(sii => sii.MedMaster)
                .ThenInclude(mm => mm.MedBase)
                .Include(si => si.OrgPlant)
                .Where(si => si.Status == "Approved");

            if (userPlantId.HasValue)
                query = query.Where(si => si.PlantId == userPlantId.Value);

            if (fromDate.HasValue)
                query = query.Where(si => si.IndentDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(si => si.IndentDate <= toDate.Value);

            var indents = await query.OrderBy(si => si.IndentDate).ToListAsync();

            var reportData = new List<StoreInventoryBatchReportDto>();

            foreach (var indent in indents)
            {
                //foreach (var item in indent.StoreIndentItems.Where(i => i.ReceivedQuantity > 0))
                foreach (var item in indent.StoreIndentItems)
                {
                    var batches = await _db.StoreIndentBatches
                        .Where(b => b.IndentItemId == item.IndentItemId)
                        .ToListAsync();

                    if (batches.Any())
                    {
                        foreach (var batch in batches)
                        {
                            var consumedStock = batch.ReceivedQuantity - batch.AvailableStock - batch.TotalDisposed;
                            var stockStatus = GetInventoryStockStatus(batch.AvailableStock, batch.ReceivedQuantity, batch.ExpiryDate, batch.TotalDisposed);

                            reportData.Add(new StoreInventoryBatchReportDto
                            {
                                IndentId = indent.IndentId,
                                RaisedDate = indent.IndentDate,
                                MedicineName = item.MedMaster?.MedItemName ?? "Unknown Medicine",
                                RaisedQuantity = item.RaisedQuantity,
                                Potency = item.MedMaster?.MedBase?.BaseName ?? "0",
                                ManufacturerBy = item.MedMaster?.CompanyName ?? "Unknown Manufacturer",
                                BatchNo = batch.BatchNo ?? "Not Set",
                                VendorCode = batch.VendorCode ?? "Not Set",
                                ReceivedQuantity = batch.ReceivedQuantity,
                                AvailableStock = batch.AvailableStock,
                                ConsumedStock = consumedStock,
                                ExpiryDate = batch.ExpiryDate,
                                RaisedBy = indent.CreatedBy ?? "Unknown",
                                StockStatus = stockStatus,
                                PlantName = indent.OrgPlant?.plant_name ?? "Unknown Plant"
                            });
                        }

                    }
                    else
                    {
                        reportData.Add(new StoreInventoryBatchReportDto
                        {
                            IndentId = indent.IndentId,
                            RaisedDate = indent.IndentDate,
                            MedicineName = item.MedMaster?.MedItemName ?? "Unknown Medicine",
                            RaisedQuantity = item.RaisedQuantity,  // full qty
                            Potency = item.MedMaster?.MedBase?.BaseName ?? "0",
                            ManufacturerBy = item.MedMaster?.CompanyName ?? "Unknown Manufacturer",
                            BatchNo = "No Batch Info",
                            VendorCode = item.VendorCode ?? "Not Set",
                            ReceivedQuantity = item.ReceivedQuantity,
                            AvailableStock = item.ReceivedQuantity,
                            ConsumedStock = 0,
                            ExpiryDate = item.ExpiryDate,
                            RaisedBy = indent.CreatedBy ?? "Unknown",
                            StockStatus = "Unknown",
                            PlantName = indent.OrgPlant?.plant_name ?? "Unknown Plant"
                        });
                    }
                }
            }

            // If a batch appears multiple times (rare), collapse to one row per batch.
            var finalData =
                reportData
                .OrderBy(r => r.MedicineName)
                .ThenBy(r => r.BatchNo)
                .ThenBy(r => r.ExpiryDate);
            return finalData;
        }
        private string GetInventoryStockStatus(int availableStock, int receivedQuantity, DateTime? expiryDate, int totalDisposed)
        {
            var today = DateTime.Today;
            var thirtyDaysFromNow = today.AddDays(30);

            // First, check expiry status (highest priority)
            if (expiryDate.HasValue)
            {
                if (expiryDate.Value.Date < today)
                {
                    // Expired medicines
                    if (availableStock == 0 && totalDisposed > 0)
                        return "Expired - Disposed (" + totalDisposed + ")";
                    else if (availableStock == 0)
                    {
                        return "Expired - Out of Stock";

                    }
                    else
                        return "Expired - Not Usable";
                }
                else if (expiryDate.Value.Date <= thirtyDaysFromNow)
                {
                    // Expiring soon (within 30 days)
                    if (availableStock == 0)
                        return "Out of Stock";
                    else if (availableStock <= (int)Math.Floor(receivedQuantity * 0.2m))
                        return "Low Stock - Expiring Soon";
                    else
                        return "In Stock - Expiring Soon";
                }
            }

            // Standard stock status logic for non-expired medicines
            if (availableStock == 0)
                return "Out of Stock";
            else if (availableStock <= (int)Math.Floor(receivedQuantity * 0.2m))
                return "Low Stock";
            else
                return "In Stock";
        }

        public async Task<IEnumerable<MedicineMasterStoreReportDto>> GetMedicineMasterStoreReportAsync(int? userPlantId = null)
        {
            var today = DateTime.Today;

            // FIXED: Start with medicines filtered by plant
            var medicinesQuery = _db.med_masters.AsQueryable();

            if (userPlantId.HasValue)
            {
                medicinesQuery = medicinesQuery.Where(m => m.plant_id == userPlantId.Value);
            }

            var allMedicines = await medicinesQuery
                .Select(m => new
                {
                    m.MedItemId,
                    m.MedItemName,
                    m.ReorderLimit,
                    m.plant_id
                })
                .ToListAsync();

            var reportData = new List<MedicineMasterStoreReportDto>();

            foreach (var medicine in allMedicines)
            {
                var stockQuery = from si in _db.StoreIndents
                                 join sii in _db.StoreIndentItems on si.IndentId equals sii.IndentId
                                 join sib in _db.StoreIndentBatches on sii.IndentItemId equals sib.IndentItemId
                                 where sii.MedItemId == medicine.MedItemId
                                       && si.Status == "Approved"
                                       && si.PlantId == medicine.plant_id  // FIXED: Use medicine's plant_id
                                 select new { sib.AvailableStock, sib.ExpiryDate, si.OrgPlant.plant_name };

                var stockData = await stockQuery.ToListAsync();

                var totalQtyInStore = stockData.Sum(x => x.AvailableStock);
                var expiredQty = stockData.Where(x => x.ExpiryDate < today).Sum(x => x.AvailableStock);
                var plantName = stockData.FirstOrDefault()?.plant_name ?? "Unknown Plant";

                reportData.Add(new MedicineMasterStoreReportDto
                {
                    MedName = medicine.MedItemName,
                    TotalQtyInStore = totalQtyInStore,
                    ExpiredQty = expiredQty,
                    ReorderLimit = medicine.ReorderLimit,
                    PlantName = plantName
                });
            }

            return reportData.OrderBy(r => r.MedName);
        }
    }
}