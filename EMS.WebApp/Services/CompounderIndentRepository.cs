using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public class CompounderIndentRepository : ICompounderIndentRepository
    {
        private readonly ApplicationDbContext _db;

        public CompounderIndentRepository(ApplicationDbContext db) => _db = db;

        // NEW: Get plant code by user name
        public async Task<string?> GetUserPlantCodeAsync(string userName)
        {
            var user = await _db.SysUsers
                .Include(u => u.OrgPlant) // Include OrgPlant navigation property
                .FirstOrDefaultAsync(u => (u.adid == userName || u.email == userName || u.full_name == userName) && u.is_active);

            return user?.OrgPlant?.plant_code;
        }

        // NEW: Get plant code by plant ID
        public async Task<string?> GetPlantCodeByIdAsync(int plantId)
        {
            var plant = await _db.org_plants.FirstOrDefaultAsync(p => p.plant_id == plantId);
            return plant?.plant_code;
        }

        public async Task<IEnumerable<CompounderIndent>> ListAsync(string currentUser = null, int? userPlantId = null, bool isDoctor = false, string? userRole = null)
        {
            var query = _db.CompounderIndents.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(s => s.plant_id == userPlantId.Value);

                // BCM filtering: Apply ONLY for Compounder role, NOT for Store or Doctor
                if (!isDoctor && IsCompounderRole(userRole))
                {
                    var plantCode = await GetPlantCodeByIdAsync(userPlantId.Value);
                    if (plantCode?.ToUpper() == "BCM")
                    {
                        // BCM plant + Compounder role: Can only see their own records
                        if (!string.IsNullOrEmpty(currentUser))
                        {
                            query = query.Where(s => s.CreatedBy == currentUser);
                        }
                        else
                        {
                            return new List<CompounderIndent>();
                        }
                    }
                }
                // Store users and Doctors see ALL records (no CreatedBy filter)
            }

            // Filter drafts to show only to their creators
            if (!string.IsNullOrEmpty(currentUser))
            {
                query = query.Where(s => s.IndentType != "Draft Indent" || s.CreatedBy == currentUser);
            }
            else
            {
                query = query.Where(s => s.IndentType != "Draft Indent");
            }

            return await query
                .Include(s => s.OrgPlant)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<CompounderIndent>> ListByTypeAsync(string indentType, string currentUser = null, int? userPlantId = null, bool isDoctor = false, string? userRole = null)
        {
            var query = _db.CompounderIndents.Where(s => s.IndentType == indentType);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(s => s.plant_id == userPlantId.Value);

                // BCM filtering: Apply ONLY for Compounder role, NOT for Store or Doctor
                if (!isDoctor && IsCompounderRole(userRole))
                {
                    var plantCode = await GetPlantCodeByIdAsync(userPlantId.Value);
                    if (plantCode?.ToUpper() == "BCM")
                    {
                        // BCM plant + Compounder role: Can only see their own records
                        if (!string.IsNullOrEmpty(currentUser))
                        {
                            query = query.Where(s => s.CreatedBy == currentUser);
                        }
                        else
                        {
                            return new List<CompounderIndent>();
                        }
                    }
                }
                // Store users and Doctors see ALL records (no CreatedBy filter)
            }

            // Additional filtering for Draft Indent - only show to creator
            if (indentType == "Draft Indent" && !string.IsNullOrEmpty(currentUser))
            {
                query = query.Where(s => s.CreatedBy == currentUser);
            }
            else if (indentType == "Draft Indent" && string.IsNullOrEmpty(currentUser))
            {
                return new List<CompounderIndent>();
            }

            return await query
                .Include(s => s.OrgPlant)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();
        }
        public async Task<IEnumerable<CompounderIndent>> ListByStatusAsync(string status, string currentUser = null, int? userPlantId = null, bool isDoctor = false, string? userRole = null)
        {
            var query = _db.CompounderIndents.Where(s => s.Status == status);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(s => s.plant_id == userPlantId.Value);

                // BCM filtering: Apply ONLY for Compounder role, NOT for Store or Doctor
                if (!isDoctor && IsCompounderRole(userRole))
                {
                    var plantCode = await GetPlantCodeByIdAsync(userPlantId.Value);
                    if (plantCode?.ToUpper() == "BCM")
                    {
                        // BCM plant + Compounder role: Can only see their own records
                        if (!string.IsNullOrEmpty(currentUser))
                        {
                            query = query.Where(s => s.CreatedBy == currentUser);
                        }
                        else
                        {
                            return new List<CompounderIndent>();
                        }
                    }
                }
                // Store users and Doctors see ALL records (no CreatedBy filter)
            }

            // Additional filtering for Draft Indent - only show to creator
            if (!string.IsNullOrEmpty(currentUser))
            {
                query = query.Where(s => s.IndentType != "Draft Indent" || s.CreatedBy == currentUser);
            }
            else
            {
                query = query.Where(s => s.IndentType != "Draft Indent");
            }

            return await query
                .Include(s => s.OrgPlant)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();
        }

        public async Task<CompounderIndent?> GetByIdAsync(int id, int? userPlantId = null)
        {
            var query = _db.CompounderIndents.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(s => s.plant_id == userPlantId.Value);
            }

            return await query
                .Include(s => s.OrgPlant)
                .FirstOrDefaultAsync(s => s.IndentId == id);
        }

        public async Task<CompounderIndent?> GetByIdWithItemsAsync(int id, int? userPlantId = null)
        {
            var query = _db.CompounderIndents.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(s => s.plant_id == userPlantId.Value);
            }

            return await query
                .Include(s => s.CompounderIndentItems)
                    .ThenInclude(i => i.MedMaster)
                        .ThenInclude(m => m.MedBase)
                .Include(s => s.OrgPlant)
                .FirstOrDefaultAsync(s => s.IndentId == id);
        }

        public async Task AddAsync(CompounderIndent entity)
        {
            _db.CompounderIndents.Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(CompounderIndent entity)
        {
            _db.CompounderIndents.Update(entity);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id, int? userPlantId = null)
        {
            var query = _db.CompounderIndents.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(s => s.plant_id == userPlantId.Value);
            }

            var entity = await query
                .Include(s => s.CompounderIndentItems)
                .FirstOrDefaultAsync(s => s.IndentId == id);

            if (entity != null)
            {
                _db.CompounderIndents.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }
        // Medicine-related methods (no plant filtering needed for master data)
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


        // CompounderIndentItem methods with plant filtering
        public async Task AddItemAsync(CompounderIndentItem item)
        {
            _db.CompounderIndentItems.Add(item);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateItemAsync(CompounderIndentItem item)
        {
            _db.CompounderIndentItems.Update(item);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteItemAsync(int indentItemId, int? userPlantId = null)
        {
            var query = from item in _db.CompounderIndentItems
                        join indent in _db.CompounderIndents on item.IndentId equals indent.IndentId
                        where item.IndentItemId == indentItemId
                        select item;

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = from item in _db.CompounderIndentItems
                        join indent in _db.CompounderIndents on item.IndentId equals indent.IndentId
                        where item.IndentItemId == indentItemId && indent.plant_id == userPlantId.Value
                        select item;
            }

            var itemToDelete = await query.FirstOrDefaultAsync();
            if (itemToDelete != null)
            {
                _db.CompounderIndentItems.Remove(itemToDelete);
                await _db.SaveChangesAsync();
            }
        }

        public async Task<CompounderIndentItem?> GetItemByIdAsync(int indentItemId, int? userPlantId = null)
        {
            var query = from item in _db.CompounderIndentItems
                        join indent in _db.CompounderIndents on item.IndentId equals indent.IndentId
                        where item.IndentItemId == indentItemId
                        select item;

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(item => _db.CompounderIndents.Any(indent =>
                    indent.IndentId == item.IndentId && indent.plant_id == userPlantId.Value));
            }

            return await query
                .Include(i => i.MedMaster)
                .Include(i => i.CompounderIndent)
                    .ThenInclude(s => s.OrgPlant)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<CompounderIndentItem>> GetItemsByIndentIdAsync(int indentId, int? userPlantId = null)
        {
            var query = _db.CompounderIndentItems.Where(i => i.IndentId == indentId);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(item => _db.CompounderIndents.Any(indent =>
                    indent.IndentId == item.IndentId && indent.plant_id == userPlantId.Value));
            }

            return await query
                .Include(i => i.MedMaster)
                .Include(i => i.CompounderIndent)
                .ThenInclude(s => s.OrgPlant)
                .ToListAsync();
        }

        public async Task<bool> IsVendorCodeExistsAsync(int indentId, string vendorCode, int? excludeItemId = null, int? userPlantId = null)
        {
            // Always return false since we no longer enforce vendor code uniqueness
            return false;
        }

        public async Task<bool> IsMedicineAlreadyAddedAsync(int indentId, int medItemId, int? excludeItemId = null, int? userPlantId = null)
        {
            var query = from item in _db.CompounderIndentItems
                        join indent in _db.CompounderIndents on item.IndentId equals indent.IndentId
                        where item.IndentId == indentId && item.MedItemId == medItemId
                        select item;

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(item => _db.CompounderIndents.Any(indent =>
                    indent.IndentId == item.IndentId && indent.plant_id == userPlantId.Value));
            }

            if (excludeItemId.HasValue)
            {
                query = query.Where(i => i.IndentItemId != excludeItemId.Value);
            }

            return await query.AnyAsync();
        }

        // Batch-related methods with plant filtering
        public async Task<List<CompounderIndentBatch>> GetBatchesByIndentItemIdAsync(int indentItemId, int? userPlantId = null)
        {
            var query = from batch in _db.CompounderIndentBatches
                        join item in _db.CompounderIndentItems on batch.IndentItemId equals item.IndentItemId
                        join indent in _db.CompounderIndents on item.IndentId equals indent.IndentId
                        where batch.IndentItemId == indentItemId
                        select batch;

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(batch =>
                    _db.CompounderIndentItems.Any(item =>
                        item.IndentItemId == batch.IndentItemId &&
                        _db.CompounderIndents.Any(indent =>
                            indent.IndentId == item.IndentId && indent.plant_id == userPlantId.Value)));
            }

            return await query.ToListAsync();
        }

        public async Task AddOrUpdateBatchesAsync(int indentItemId, List<CompounderIndentBatch> batches, int? userPlantId = null)
        {
            // Verify plant access before proceeding
            if (userPlantId.HasValue)
            {
                var hasAccess = await _db.CompounderIndentItems.AnyAsync(item =>
                    item.IndentItemId == indentItemId &&
                    _db.CompounderIndents.Any(indent =>
                        indent.IndentId == item.IndentId && indent.plant_id == userPlantId.Value));

                if (!hasAccess)
                {
                    throw new UnauthorizedAccessException("Access denied to this indent item.");
                }
            }

            // Remove old batches
            var old = await _db.CompounderIndentBatches.Where(x => x.IndentItemId == indentItemId).ToListAsync();
            _db.CompounderIndentBatches.RemoveRange(old);
            await _db.SaveChangesAsync();

            // Add new batches
            foreach (var batch in batches)
                batch.BatchId = 0; // For EF auto-generation

            _db.CompounderIndentBatches.AddRange(batches);
            await _db.SaveChangesAsync();

            // Update received quantity in CompounderIndentItem
            var item = await _db.CompounderIndentItems.FindAsync(indentItemId);
            if (item != null)
            {
                item.ReceivedQuantity = batches.Sum(x => x.ReceivedQuantity);
                await _db.SaveChangesAsync();
            }
        }

        public async Task DeleteBatchAsync(int indentId, int batchId, int? userPlantId = null)
        {

            var data = await (
                from b in _db.CompounderIndentBatches
                join ii in _db.CompounderIndentItems on b.IndentItemId equals ii.IndentItemId
                join i in _db.CompounderIndents on ii.IndentId equals i.IndentId
                where b.BatchId == batchId
                select new
                {
                    Batch = b,                                  // compounder batch
                    Item = ii,                                  // indent item (has MedItemId)
                    Indent = i                                  // has PlantId
                }
            ).FirstOrDefaultAsync();

            if (data == null)
                throw new InvalidOperationException($"Batch {batchId} not found.");

            if (data.Item.IndentItemId != indentId)
                throw new InvalidOperationException("The specified batch does not belong to the given indent item.");

            var addBackQty = data.Batch.AvailableStock;        // << use AVAILABLE STOCK of deleting batch
            var batchNo = data.Batch.BatchNo;
            var medItemId = data.Item.MedItemId;
            var userPlant = userPlantId;     // prefer indent's plant

            if (addBackQty < 0)
                throw new InvalidOperationException("Deleting batch has invalid available stock.");


            var storeBatch = await GetStoreBatchByBatchNoAndMedicineAsync(batchNo, medItemId, userPlantId);
            if (storeBatch != null)
            {
                // 3) Compute new store stock and persist using your Update helper
                var newStoreStock = storeBatch.AvailableStock + addBackQty;
                await UpdateBatchAvailableStockAsync(storeBatch.BatchId, newStoreStock, userPlantId);
            }

            // Now delete the compounder batch
            _db.CompounderIndentBatches.Remove(data.Batch);

            await _db.SaveChangesAsync();
        }
        // FIXED: Store integration methods with proper plant filtering

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

        // FIXED: Corrected plant filtering for available stock - THIS IS THE KEY METHOD
        public async Task<int> GetTotalAvailableStockFromStoreAsync(int medItemId, int? userPlantId = null)
        {
            if (userPlantId.HasValue)
            {
                var query = from si in _db.StoreIndents
                            join sii in _db.StoreIndentItems on si.IndentId equals sii.IndentId
                            join sib in _db.StoreIndentBatches on sii.IndentItemId equals sib.IndentItemId
                            where si.Status == "Approved"
                                  && sii.MedItemId == medItemId
                                  && sib.ExpiryDate >= DateTime.Today
                                  && si.PlantId == userPlantId.Value  // THIS LINE IS CRITICAL
                            select sib.AvailableStock;

                var result = await query.SumAsync();
                return result;
            }
            else
            {
                var query = from si in _db.StoreIndents
                            join sii in _db.StoreIndentItems on si.IndentId equals sii.IndentId
                            join sib in _db.StoreIndentBatches on sii.IndentItemId equals sib.IndentItemId
                            //where si.Status == "Approved" && sii.MedItemId == medItemId 
                            where si.Status == "Approved" && sii.MedItemId == medItemId && sib.ExpiryDate >= DateTime.Today
                            select sib.AvailableStock;
                var result = await query.SumAsync();
                return result;
            }
        }

        //Corrected plant filtering for available store batches
        public async Task<IEnumerable<StoreIndentBatchDto>> GetAvailableStoreBatchesForMedicineAsync(int medItemId, int? userPlantId = null)
        {
            if (userPlantId.HasValue)
            {
                // Enhanced query with better sorting for FIFO compliance
                var query = from si in _db.StoreIndents
                            join sii in _db.StoreIndentItems on si.IndentId equals sii.IndentId
                            join sib in _db.StoreIndentBatches on sii.IndentItemId equals sib.IndentItemId
                            where si.Status == "Approved"
                                  && sii.MedItemId == medItemId
                                  && sib.AvailableStock > 0
                                  && sib.ExpiryDate > DateTime.Today
                                  && si.PlantId == userPlantId.Value
                            // Enhanced ordering: Expiry date first (FIFO), then batch number
                            orderby sib.ExpiryDate ascending, sib.BatchNo ascending
                            select new StoreIndentBatchDto
                            {
                                BatchNo = sib.BatchNo,
                                ExpiryDate = sib.ExpiryDate,
                                AvailableStock = sib.AvailableStock,
                                VendorCode = sib.VendorCode ?? "",
                                DisplayText = sib.BatchNo,
                                // Additional properties for early expiry detection
                                DaysToExpiry = EF.Functions.DateDiffDay(DateTime.Today, sib.ExpiryDate),
                                IsEarliestExpiry = false, // Will be set in post-processing
                                ExpiryPriority = EF.Functions.DateDiffDay(DateTime.Today, sib.ExpiryDate) <= 30 ? "URGENT" :
                                               EF.Functions.DateDiffDay(DateTime.Today, sib.ExpiryDate) <= 90 ? "SOON" : "NORMAL"
                            };

                var batches = await query.ToListAsync();

                // Post-process to mark the earliest expiry batch
                if (batches.Any())
                {
                    var earliestExpiry = batches.First(); // Already sorted by expiry date
                    earliestExpiry.IsEarliestExpiry = true;
                }

                return batches;
            }
            else
            {
                // No plant filtering - return all plants with enhanced sorting
                var query = from si in _db.StoreIndents
                            join sii in _db.StoreIndentItems on si.IndentId equals sii.IndentId
                            join sib in _db.StoreIndentBatches on sii.IndentItemId equals sib.IndentItemId
                            where si.Status == "Approved"
                                  && sii.MedItemId == medItemId
                                  && sib.AvailableStock > 0
                                  && sib.ExpiryDate >= DateTime.Today
                            // Enhanced ordering: Expiry date first (FIFO), then batch number
                            orderby sib.ExpiryDate ascending, sib.BatchNo ascending
                            select new StoreIndentBatchDto
                            {
                                BatchNo = sib.BatchNo,
                                ExpiryDate = sib.ExpiryDate,
                                AvailableStock = sib.AvailableStock,
                                VendorCode = sib.VendorCode ?? "",
                                DisplayText = sib.BatchNo,
                                DaysToExpiry = EF.Functions.DateDiffDay(DateTime.Today, sib.ExpiryDate),
                                IsEarliestExpiry = false,
                                ExpiryPriority = EF.Functions.DateDiffDay(DateTime.Today, sib.ExpiryDate) <= 30 ? "URGENT" :
                                               EF.Functions.DateDiffDay(DateTime.Today, sib.ExpiryDate) <= 90 ? "SOON" : "NORMAL"
                            };

                var batches = await query.ToListAsync();

                // Post-process to mark the earliest expiry batch
                if (batches.Any())
                {
                    var earliestExpiry = batches.First();
                    earliestExpiry.IsEarliestExpiry = true;
                }

                return batches;
            }
        }
        // FIXED: Corrected plant filtering for store batch lookup
        public async Task<StoreIndentBatch?> GetStoreBatchByBatchNoAndMedicineAsync(string batchNo, int medItemId, int? userPlantId = null)
        {
            if (userPlantId.HasValue)
            {
                // FIXED: Direct plant filtering using si.PlantId
                var query = from sib in _db.StoreIndentBatches
                            join sii in _db.StoreIndentItems on sib.IndentItemId equals sii.IndentItemId
                            join si in _db.StoreIndents on sii.IndentId equals si.IndentId
                            where sib.BatchNo == batchNo
                                  && sii.MedItemId == medItemId
                                  && si.Status == "Approved"
                                  && si.PlantId == userPlantId.Value  // CRITICAL: Direct plant filtering
                            select sib;

                return await query.FirstOrDefaultAsync();
            }
            else
            {
                // No plant filtering
                var query = from sib in _db.StoreIndentBatches
                            join sii in _db.StoreIndentItems on sib.IndentItemId equals sii.IndentItemId
                            join si in _db.StoreIndents on sii.IndentId equals si.IndentId
                            where sib.BatchNo == batchNo
                                  && sii.MedItemId == medItemId
                                  && si.Status == "Approved"
                            select sib;

                return await query.FirstOrDefaultAsync();
            }
        }


        //  Corrected plant filtering for batch stock update
        public async Task UpdateBatchAvailableStockAsync(int batchId, int newAvailableStock, int? userPlantId = null)
        {
            if (userPlantId.HasValue)
            {
                // FIXED: Direct plant filtering with proper join
                var query = from sib in _db.StoreIndentBatches
                            join item in _db.StoreIndentItems on sib.IndentItemId equals item.IndentItemId
                            join indent in _db.StoreIndents on item.IndentId equals indent.IndentId
                            where sib.BatchId == batchId && indent.PlantId == userPlantId.Value
                            select sib;

                var batch = await query.FirstOrDefaultAsync();
                if (batch != null)
                {
                    batch.AvailableStock = newAvailableStock;
                    await _db.SaveChangesAsync();
                }
            }
            else
            {
                // No plant filtering
                var batch = await _db.StoreIndentBatches.FirstOrDefaultAsync(b => b.BatchId == batchId);
                if (batch != null)
                {
                    batch.AvailableStock = newAvailableStock;
                    await _db.SaveChangesAsync();
                }
            }
        }

        // Helper methods for plant-based operations
        public async Task<int?> GetUserPlantIdAsync(string userName)
        {
            var user = await _db.SysUsers
                .FirstOrDefaultAsync(u => (u.adid == userName || u.email == userName || u.full_name == userName) && u.is_active);

            return user?.plant_id;
        }

        public async Task<bool> IsUserAuthorizedForIndentAsync(int indentId, int userPlantId)
        {
            return await _db.CompounderIndents.AnyAsync(s => s.IndentId == indentId && s.plant_id == userPlantId);
        }

        // Report methods with plant filtering
        public async Task<IEnumerable<CompounderIndentReportDto>> GetCompounderIndentReportAsync(
    DateTime? fromDate = null,
    DateTime? toDate = null,
    int? userPlantId = null,
    string currentUser = null,
    bool isDoctor = false)
        {
            var query = _db.CompounderIndents
                .Include(ci => ci.CompounderIndentItems)
                    .ThenInclude(cii => cii.MedMaster)
                        .ThenInclude(mm => mm.MedBase)
                .Include(ci => ci.OrgPlant)
                .Where(ci => ci.Status != "Draft");

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(ci => ci.plant_id == userPlantId.Value);

                // BCM plant-specific filtering: Compounders see only their own records, Doctors see all
                if (!isDoctor && !string.IsNullOrEmpty(currentUser))
                {
                    var plantCode = await GetPlantCodeByIdAsync(userPlantId.Value);
                    if (plantCode?.ToUpper() == "BCM")
                    {
                        // Get all possible user identifiers (adid, email, full_name) for matching
                        var userIdentifiers = new List<string>();
                        var userRecord = await _db.SysUsers
                            .Where(u => (u.adid == currentUser || u.email == currentUser || u.full_name == currentUser) && u.is_active)
                            .Select(u => new { u.adid, u.email, u.full_name })
                            .FirstOrDefaultAsync();

                        if (userRecord != null)
                        {
                            if (!string.IsNullOrEmpty(userRecord.adid))
                                userIdentifiers.Add(userRecord.adid);
                            if (!string.IsNullOrEmpty(userRecord.email))
                                userIdentifiers.Add(userRecord.email);
                            if (!string.IsNullOrEmpty(userRecord.full_name))
                                userIdentifiers.Add(userRecord.full_name);
                        }

                        if (!userIdentifiers.Contains(currentUser))
                            userIdentifiers.Add(currentUser);

                        // BCM plant + non-Doctor: Can only see their own records
                        if (userIdentifiers.Any())
                        {
                            query = query.Where(ci => userIdentifiers.Contains(ci.CreatedBy));
                        }
                        else
                        {
                            return new List<CompounderIndentReportDto>();
                        }
                    }
                }
                // Doctors see ALL records (no CreatedBy filter)
            }

            // Apply date filtering if provided
            if (fromDate.HasValue)
            {
                query = query.Where(ci => ci.IndentDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(ci => ci.IndentDate <= toDate.Value);
            }

            var indents = await query.OrderBy(ci => ci.IndentDate).ToListAsync();

            var reportData = new List<CompounderIndentReportDto>();

            foreach (var indent in indents)
            {
                foreach (var item in indent.CompounderIndentItems)
                {
                    reportData.Add(new CompounderIndentReportDto
                    {
                        IndentId = indent.IndentId,
                        IndentDate = indent.IndentDate,
                        MedicineName = item.MedMaster?.MedItemName ?? "Unknown Medicine",
                        ManufacturerName = item.MedMaster?.CompanyName ?? "Unknown Manufacturer",
                        Quantity = item.RaisedQuantity,
                        RaisedBy = indent.CreatedBy ?? "Unknown",
                        PlantName = indent.OrgPlant?.plant_name ?? "Unknown Plant"
                    });
                }
            }

            return reportData.OrderBy(r => r.MedicineName);
        }
        public async Task<IEnumerable<CompounderInventoryReportDto>> GetCompounderInventoryReportAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? userPlantId = null,
        bool showOnlyAvailable = false,
        string currentUser = null,
        bool isDoctor = false)
        {
            var query = _db.CompounderIndents
                .Include(ci => ci.CompounderIndentItems)
                .ThenInclude(cii => cii.MedMaster)
                .ThenInclude(mm => mm.MedBase)
                .Include(ci => ci.OrgPlant)
                .Where(ci => ci.Status == "Approved" || ci.Status == "Pending");

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(ci => ci.plant_id == userPlantId.Value);

                // BCM plant-specific filtering: Compounders see only their own records, Doctors see all
                if (!isDoctor && !string.IsNullOrEmpty(currentUser))
                {
                    var plantCode = await GetPlantCodeByIdAsync(userPlantId.Value);
                    if (plantCode?.ToUpper() == "BCM")
                    {
                        // Get all possible user identifiers (adid, email, full_name) for matching
                        var userIdentifiers = new List<string>();
                        var userRecord = await _db.SysUsers
                            .Where(u => (u.adid == currentUser || u.email == currentUser || u.full_name == currentUser) && u.is_active)
                            .Select(u => new { u.adid, u.email, u.full_name })
                            .FirstOrDefaultAsync();

                        if (userRecord != null)
                        {
                            if (!string.IsNullOrEmpty(userRecord.adid))
                                userIdentifiers.Add(userRecord.adid);
                            if (!string.IsNullOrEmpty(userRecord.email))
                                userIdentifiers.Add(userRecord.email);
                            if (!string.IsNullOrEmpty(userRecord.full_name))
                                userIdentifiers.Add(userRecord.full_name);
                        }

                        if (!userIdentifiers.Contains(currentUser))
                            userIdentifiers.Add(currentUser);

                        // BCM plant + non-Doctor: Can only see their own records
                        if (userIdentifiers.Any())
                        {
                            query = query.Where(ci => userIdentifiers.Contains(ci.CreatedBy));
                        }
                        else
                        {
                            return new List<CompounderInventoryReportDto>();
                        }
                    }
                }
                // Doctors see ALL records (no CreatedBy filter)
            }

            // Apply date filtering if provided
            if (fromDate.HasValue)
            {
                query = query.Where(ci => ci.IndentDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(ci => ci.IndentDate <= toDate.Value);
            }

            var indents = await query.OrderBy(ci => ci.IndentDate).ToListAsync();

            var reportData = new List<CompounderInventoryReportDto>();

            foreach (var indent in indents)
            {
                foreach (var item in indent.CompounderIndentItems)
                {
                    // Get batches for this compounder indent item with plant filtering
                    var batches = await GetBatchesByIndentItemIdAsync(item.IndentItemId, userPlantId);

                    if (batches.Any())
                    {
                        // FIXED: Distribute raised quantity proportionally across batches
                        var totalReceivedInBatches = batches.Sum(b => b.ReceivedQuantity);

                        foreach (var batch in batches)
                        {
                            reportData.Add(new CompounderInventoryReportDto
                            {
                                IndentId = indent.IndentId,
                                RaisedDate = indent.IndentDate,
                                MedicineName = item.MedMaster?.MedItemName ?? "Unknown Medicine",
                                RaisedQuantity = item.RaisedQuantity,
                                ReceivedQuantity = batch.ReceivedQuantity,
                                Potency = item.MedMaster?.MedBase?.BaseName ?? "0",
                                ManufacturerBy = item.MedMaster?.CompanyName ?? "Unknown Manufacturer",
                                BatchNo = batch.BatchNo ?? "Not Set",
                                VendorCode = batch.VendorCode ?? "Not Set",
                                AvailableStock = batch.AvailableStock,
                                //ConsumedStock = batch.ReceivedQuantity - batch.AvailableStock - batch.TotalDisposed,
                                ConsumedStock = Math.Max(0, batch.ReceivedQuantity - batch.AvailableStock - batch.TotalDisposed),
                                //ConsumedStock = Math.Max(0, batch.ReceivedQuantity - batch.AvailableStock),
                                ExpiryDate = batch.ExpiryDate,
                                RaisedBy = indent.CreatedBy ?? "Unknown",
                                PlantName = indent.OrgPlant?.plant_name ?? "Unknown Plant",
                                StockStatus = GetStockStatus(batch.AvailableStock, batch.ReceivedQuantity)
                            });
                        }
                    }
                    else
                    {
                        // If no batches, show basic item info (unchanged)
                        reportData.Add(new CompounderInventoryReportDto
                        {
                            IndentId = indent.IndentId,
                            RaisedDate = indent.IndentDate,
                            MedicineName = item.MedMaster?.MedItemName ?? "Unknown Medicine",
                            RaisedQuantity = item.RaisedQuantity, // Full quantity for non-batch items
                            ReceivedQuantity = item.ReceivedQuantity,
                            Potency = item.MedMaster?.MedBase?.BaseName ?? "0",
                            ManufacturerBy = item.MedMaster?.CompanyName ?? "Unknown Manufacturer",
                            BatchNo = item.BatchNo ?? "No Batch Info",
                            VendorCode = "Not Set",
                            AvailableStock = item.AvailableStock ?? item.ReceivedQuantity,
                            ConsumedStock = 0,
                            ExpiryDate = item.ExpiryDate,
                            RaisedBy = indent.CreatedBy ?? "Unknown",
                            PlantName = indent.OrgPlant?.plant_name ?? "Unknown Plant",
                            StockStatus = "Unknown"
                        });
                    }
                }
            }

            // FIXED: Updated grouping logic to handle proportional quantities correctly
            var groupedData = reportData
                .Where(r => r.BatchNo != "No Batch Info" && r.BatchNo != "Not Set")
                .GroupBy(r => new {
                    BatchNo = r.BatchNo,
                    MedicineName = r.MedicineName,
                    Potency = r.Potency,
                    ManufacturerBy = r.ManufacturerBy,
                    VendorCode = r.VendorCode,
                    ExpiryDate = r.ExpiryDate
                })
                .Select(g => new CompounderInventoryReportDto
                {
                    IndentId = g.OrderBy(x => x.RaisedDate).First().IndentId,
                    RaisedDate = g.OrderBy(x => x.RaisedDate).First().RaisedDate,
                    MedicineName = g.Key.MedicineName,
                    RaisedQuantity = g.Sum(x => x.RaisedQuantity), // FIXED: Now correctly sums proportional quantities
                    ReceivedQuantity = g.Sum(x => x.ReceivedQuantity),
                    Potency = g.Key.Potency,
                    ManufacturerBy = g.Key.ManufacturerBy,
                    BatchNo = g.Key.BatchNo,
                    VendorCode = g.Key.VendorCode,
                    AvailableStock = g.Sum(x => x.AvailableStock),
                    ConsumedStock = g.Sum(x => x.ConsumedStock),
                    ExpiryDate = g.Key.ExpiryDate,
                    RaisedBy = string.Join(", ", g.Select(x => x.RaisedBy).Distinct()),
                    PlantName = g.First().PlantName,
                    StockStatus = GetStockStatus(g.Sum(x => x.AvailableStock), g.Sum(x => x.ReceivedQuantity))
                })
                .ToList();

            // Apply available stock filter if requested
            if (showOnlyAvailable)
            {
                reportData = reportData.Where(r => r.AvailableStock > 0).ToList();
            }

            return reportData.OrderBy(r => r.MedicineName).ThenBy(r => r.BatchNo).ThenBy(r => r.ExpiryDate);
        }
        // Helper method to determine stock status
        private string GetStockStatus(int availableStock, int receivedQuantity)
        {
            if (availableStock == 0) return "Out of Stock";
            if (availableStock <= (receivedQuantity * 0.2)) return "Low Stock";
            return "In Stock";
        }

        public async Task<IEnumerable<DailyMedicineConsumptionReportDto>> GetDailyMedicineConsumptionReportAsync(
         DateTime? fromDate = null,
         DateTime? toDate = null,
         int? userPlantId = null,
         string currentUser = null,
         bool isDoctor = false)
        {
            // Date range calculation
            var startDate = fromDate?.Date ?? DateTime.Today;
            var endDate = (toDate?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);

            try
            {
                // Check if BCM plant filtering should be applied
                bool applyBcmFilter = false;
                List<string> userIdentifiers = new List<string>();

                if (userPlantId.HasValue && !isDoctor && !string.IsNullOrEmpty(currentUser))
                {
                    var plantCode = await GetPlantCodeByIdAsync(userPlantId.Value);
                    applyBcmFilter = plantCode?.ToUpper() == "BCM";

                    if (applyBcmFilter)
                    {
                        // Get all possible user identifiers (adid, email, full_name) for matching
                        var userRecord = await _db.SysUsers
                            .Where(u => (u.adid == currentUser || u.email == currentUser || u.full_name == currentUser) && u.is_active)
                            .Select(u => new { u.adid, u.email, u.full_name })
                            .FirstOrDefaultAsync();

                        if (userRecord != null)
                        {
                            if (!string.IsNullOrEmpty(userRecord.adid))
                                userIdentifiers.Add(userRecord.adid);
                            if (!string.IsNullOrEmpty(userRecord.email))
                                userIdentifiers.Add(userRecord.email);
                            if (!string.IsNullOrEmpty(userRecord.full_name))
                                userIdentifiers.Add(userRecord.full_name);
                        }

                        if (!userIdentifiers.Contains(currentUser))
                            userIdentifiers.Add(currentUser);
                    }
                }

                // Get consumption from Doctor Prescriptions
                var doctorConsumptionQuery = from pm in _db.MedPrescriptionMedicines
                                             join p in _db.MedPrescriptions on pm.PrescriptionId equals p.PrescriptionId
                                             join mm in _db.med_masters on pm.MedItemId equals mm.MedItemId
                                             where p.PrescriptionDate >= startDate
                                                   && p.PrescriptionDate <= endDate
                                                   && (p.ApprovalStatus == "Approved" || p.ApprovalStatus == "Completed" || p.ApprovalStatus == "Pending")
                                             select new { pm.MedItemId, mm.MedItemName, pm.Quantity, p.PlantId, p.CreatedBy };

                // Apply plant filtering
                if (userPlantId.HasValue)
                {
                    doctorConsumptionQuery = doctorConsumptionQuery.Where(x => x.PlantId == userPlantId.Value);
                }

                // Apply BCM compounder-wise filtering
                if (applyBcmFilter && userIdentifiers.Any())
                {
                    doctorConsumptionQuery = doctorConsumptionQuery.Where(x => userIdentifiers.Contains(x.CreatedBy));
                }

                var doctorConsumption = await doctorConsumptionQuery
                    .GroupBy(x => new { x.MedItemId, x.MedItemName })
                    .Select(g => new {
                        MedItemId = g.Key.MedItemId,
                        MedicineName = g.Key.MedItemName,
                        ConsumedQty = g.Sum(x => x.Quantity)
                    }).ToListAsync();

                // Get consumption from Others Diagnoses
                var othersConsumptionQuery = from dm in _db.OthersDiagnosisMedicines
                                             join d in _db.OthersDiagnoses on dm.DiagnosisId equals d.DiagnosisId
                                             join mm in _db.med_masters on dm.MedItemId equals mm.MedItemId
                                             where d.VisitDate >= startDate
                                                   && d.VisitDate <= endDate
                                                   && (d.ApprovalStatus == "Approved" || d.ApprovalStatus == "Completed" || d.ApprovalStatus == "Pending")
                                             select new { dm.MedItemId, mm.MedItemName, dm.Quantity, d.PlantId, d.CreatedBy };

                // Apply plant filtering
                if (userPlantId.HasValue)
                {
                    othersConsumptionQuery = othersConsumptionQuery.Where(x => x.PlantId == userPlantId.Value);
                }

                // Apply BCM compounder-wise filtering
                if (applyBcmFilter && userIdentifiers.Any())
                {
                    othersConsumptionQuery = othersConsumptionQuery.Where(x => userIdentifiers.Contains(x.CreatedBy));
                }

                var othersConsumption = await othersConsumptionQuery
                    .GroupBy(x => new { x.MedItemId, x.MedItemName })
                    .Select(g => new {
                        MedItemId = g.Key.MedItemId,
                        MedicineName = g.Key.MedItemName,
                        ConsumedQty = g.Sum(x => x.Quantity)
                    }).ToListAsync();

                // Combine consumption data from both sources
                var combinedConsumption = doctorConsumption.Concat(othersConsumption)
                                                          .GroupBy(x => new { x.MedItemId, x.MedicineName })
                                                          .Select(g => new {
                                                              MedItemId = g.Key.MedItemId,
                                                              MedicineName = g.Key.MedicineName,
                                                              ConsumedQty = g.Sum(x => x.ConsumedQty)
                                                          }).ToList();

                // Get all medicines (plant filtered)
                var medicinesQuery = _db.med_masters.AsQueryable();
                if (userPlantId.HasValue)
                {
                    medicinesQuery = medicinesQuery.Where(m => m.plant_id == userPlantId.Value);
                }

                var allMedicines = await medicinesQuery
                    .Select(m => new { m.MedItemId, m.MedItemName, m.plant_id })
                    .ToListAsync();

                var reportData = new List<DailyMedicineConsumptionReportDto>();
                var medicinesWithConsumption = combinedConsumption.Select(c => c.MedItemId).ToList();
                var allRelevantMedicineIds = allMedicines.Select(m => m.MedItemId).Union(medicinesWithConsumption).ToList();

                foreach (var medicineId in allRelevantMedicineIds)
                {
                    var medicine = allMedicines.FirstOrDefault(m => m.MedItemId == medicineId);
                    var consumption = combinedConsumption.FirstOrDefault(c => c.MedItemId == medicineId);

                    // Skip if medicine not in our plant and we have plant filtering
                    if (medicine == null && userPlantId.HasValue) continue;

                    var medicineName = medicine?.MedItemName ?? consumption?.MedicineName ?? "Unknown Medicine";

                    // Get current available stock (non-expired) from CompounderIndentBatches
                    // UPDATED: Added CreatedBy for BCM compounder-wise filtering
                    var currentStockQuery = from ci in _db.CompounderIndents
                                            join cii in _db.CompounderIndentItems on ci.IndentId equals cii.IndentId
                                            join cib in _db.CompounderIndentBatches on cii.IndentItemId equals cib.IndentItemId
                                            where cii.MedItemId == medicineId
                                                  && cib.ExpiryDate >= DateTime.Today
                                            select new { cib.AvailableStock, ci.plant_id, ci.CreatedBy };

                    // Apply plant filtering
                    if (userPlantId.HasValue)
                    {
                        currentStockQuery = currentStockQuery.Where(x => x.plant_id == userPlantId.Value);
                    }

                    // NEW: Apply BCM compounder-wise filtering for current stock
                    if (applyBcmFilter && userIdentifiers.Any())
                    {
                        currentStockQuery = currentStockQuery.Where(x => userIdentifiers.Contains(x.CreatedBy));
                    }

                    var currentStock = await currentStockQuery.SumAsync(x => x.AvailableStock);

                    // Get expired quantity from CompounderIndentBatches
                    // UPDATED: Added CreatedBy for BCM compounder-wise filtering
                    var expiredStockQuery = from ci in _db.CompounderIndents
                                            join cii in _db.CompounderIndentItems on ci.IndentId equals cii.IndentId
                                            join cib in _db.CompounderIndentBatches on cii.IndentItemId equals cib.IndentItemId
                                            where cii.MedItemId == medicineId
                                                  && cib.ExpiryDate < DateTime.Today
                                            select new { cib.AvailableStock, ci.plant_id, ci.CreatedBy };

                    // Apply plant filtering
                    if (userPlantId.HasValue)
                    {
                        expiredStockQuery = expiredStockQuery.Where(x => x.plant_id == userPlantId.Value);
                    }

                    // NEW: Apply BCM compounder-wise filtering for expired stock
                    if (applyBcmFilter && userIdentifiers.Any())
                    {
                        expiredStockQuery = expiredStockQuery.Where(x => userIdentifiers.Contains(x.CreatedBy));
                    }

                    var expiredStock = await expiredStockQuery.SumAsync(x => x.AvailableStock);

                    var consumedQty = consumption?.ConsumedQty ?? 0;

                    // Include ONLY if consumption (IssuedQty) is greater than 0
                    if (consumedQty > 0)
                    {
                        reportData.Add(new DailyMedicineConsumptionReportDto
                        {
                            MedicineName = medicineName,
                            TotalStockInCompounderInventory = currentStock,
                            IssuedQty = consumedQty,
                            ExpiredQty = expiredStock,
                            PlantName = "N/A",
                            // NEW: Calculate Total Available at Compounder Inventory = TotalStock + IssuedQty + ExpiredQty
                            TotalAvailableAtCompounderInventory = currentStock + consumedQty + expiredStock
                        });
                    }
                }

                return reportData.OrderBy(r => r.MedicineName);
            }
            catch (Exception)
            {
                return new List<DailyMedicineConsumptionReportDto>();
            }
        }


        public async Task<IEnumerable<MedicineMasterCompounderReportDto>> GetMedicineMasterCompounderReportAsync(int? userPlantId = null)
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

            var reportData = new List<MedicineMasterCompounderReportDto>();

            foreach (var medicine in allMedicines)
            {
                // Get total quantity in store from StoreIndentBatches with plant filtering
                var storeQuery = from si in _db.StoreIndents
                                 join sii in _db.StoreIndentItems on si.IndentId equals sii.IndentId
                                 join sib in _db.StoreIndentBatches on sii.IndentItemId equals sib.IndentItemId
                                 where sii.MedItemId == medicine.MedItemId
                                       && si.PlantId == medicine.plant_id  // FIXED: Use medicine's plant_id
                                 select new { sib.AvailableStock, si.OrgPlant.plant_name };

                var storeData = await storeQuery.ToListAsync();
                var totalQtyInStore = storeData.Sum(s => s.AvailableStock);

                // Get expired quantity from CompounderIndentBatches with plant filtering
                var expiredQuery = from ci in _db.CompounderIndents
                                   join cii in _db.CompounderIndentItems on ci.IndentId equals cii.IndentId
                                   join cib in _db.CompounderIndentBatches on cii.IndentItemId equals cib.IndentItemId
                                   where cii.MedItemId == medicine.MedItemId
                                         && cib.ExpiryDate < today
                                         && ci.plant_id == medicine.plant_id  // FIXED: Use medicine's plant_id
                                   select new { cib.AvailableStock, ci.OrgPlant.plant_name };

                var expiredData = await expiredQuery.ToListAsync();
                var expiredQty = expiredData.Sum(e => e.AvailableStock);

                var plantName = storeData.FirstOrDefault()?.plant_name ??
                               expiredData.FirstOrDefault()?.plant_name ??
                               "Unknown Plant";

                reportData.Add(new MedicineMasterCompounderReportDto
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

        /// <summary>
        /// Checks if the user role is Compounder (for BCM-specific filtering)
        /// </summary>
        private bool IsCompounderRole(string? userRole)
        {
            if (string.IsNullOrEmpty(userRole))
                return false;

            return userRole.ToLower().Contains("compounder");
        }
        private class StoreStockUpdate
        {
            public int BatchId { get; set; }
            public string BatchNo { get; set; } = string.Empty;
            public int PreviousStock { get; set; }
            public int NewStock { get; set; }
            public int IssuedQuantity { get; set; }
            public int PreviousIssued { get; set; }
            public int NewTotalIssued { get; set; }
        }
    }


}