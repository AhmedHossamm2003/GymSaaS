using GymSaaS.Models;
using GymSaaS.Persistence;
using GymSaaS.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymSaaS.Controllers
{
    [Authorize(Policy = "ManagerAndAbove")]
    public class ExercisesController : Controller
    {
        private readonly GymDbContext _db;
        private readonly IWebHostEnvironment _env;

        public ExercisesController(GymDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private Guid TenantId =>
            Guid.Parse(User.FindFirstValue("TenantId")!);

        private Guid UserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // GET /Exercises
        public async Task<IActionResult> Index(string? search, int? categoryId, byte? difficulty, bool? activeOnly)
        {
            var query = _db.Exercises
                .Where(e => e.TenantId == TenantId && !e.IsDeleted);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(e =>
                    e.ExerciseName.Contains(search) ||
                    (e.Equipment != null && e.Equipment.Contains(search)));

            if (categoryId.HasValue)
                query = query.Where(e => e.ExerciseCategoryId == categoryId.Value);

            if (difficulty.HasValue)
                query = query.Where(e => e.Difficulty == difficulty.Value);

            if (activeOnly == true)
                query = query.Where(e => e.IsActive);

            var raw = await query
                .OrderByDescending(e => e.CreatedAtUtc)
                .Select(e => new
                {
                    e.ExerciseId,
                    e.ExerciseName,
                    CategoryName = e.Category.CategoryName,
                    CategoryIconClass = e.Category.IconClass,
                    e.Difficulty,
                    e.Equipment,
                    e.PhotoUrl,
                    e.VideoUrl,
                    e.IsActive,
                    e.CreatedAtUtc,
                })
                .ToListAsync();

            var exerciseIds = raw.Select(e => e.ExerciseId).ToList();
            var muscleGroupLinks = await _db.ExerciseMuscleGroups
                .Where(emg => exerciseIds.Contains(emg.ExerciseId))
                .Select(emg => new { emg.ExerciseId, emg.MuscleGroup.MuscleGroupName })
                .ToListAsync();

            var musclesByExercise = muscleGroupLinks
                .GroupBy(m => m.ExerciseId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.MuscleGroupName).ToList());

            var exercises = raw.Select(e => new ExerciseListItem
            {
                ExerciseId = e.ExerciseId,
                ExerciseName = e.ExerciseName,
                CategoryName = e.CategoryName,
                CategoryIconClass = e.CategoryIconClass,
                Difficulty = e.Difficulty,
                Equipment = e.Equipment,
                PhotoUrl = e.PhotoUrl,
                VideoUrl = e.VideoUrl,
                IsActive = e.IsActive,
                CreatedAtUtc = e.CreatedAtUtc,
                MuscleGroupNames = musclesByExercise.TryGetValue(e.ExerciseId, out var mgs) ? mgs : new(),
            }).ToList();

            ViewData["Title"] = "Exercise Library";
            ViewData["Search"] = search;
            ViewData["CategoryId"] = categoryId;
            ViewData["Difficulty"] = difficulty;
            ViewData["ActiveOnly"] = activeOnly;
            ViewData["Categories"] = await _db.ExerciseCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.ExerciseCategoryId)
                .ToListAsync();
            ViewData["TotalCount"] = exercises.Count;
            ViewData["ActiveCount"] = exercises.Count(e => e.IsActive);

            return View(exercises);
        }

        // GET /Exercises/Create
        public async Task<IActionResult> Create()
        {
            var vm = new ExerciseFormViewModel
            {
                Categories = await GetCategoriesAsync(),
                AllMuscleGroups = await GetMuscleGroupsAsync(),
            };
            ViewData["Title"] = "Exercise Library";
            ViewData["Subtitle"] = "New Exercise";
            return View("CreateEdit", vm);
        }

        // POST /Exercises/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ExerciseFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Categories = await GetCategoriesAsync();
                model.AllMuscleGroups = await GetMuscleGroupsAsync();
                ViewData["Title"] = "Exercise Library";
                ViewData["Subtitle"] = "New Exercise";
                return View("CreateEdit", model);
            }

            var exerciseId = Guid.NewGuid();
            var photoUrl = await SavePhotoAsync(model.Photo, exerciseId);

            var exercise = new Exercise
            {
                ExerciseId = exerciseId,
                TenantId = TenantId,
                ExerciseName = model.ExerciseName.Trim(),
                Description = model.Description?.Trim(),
                Instructions = model.Instructions?.Trim(),
                PhotoUrl = photoUrl,
                VideoUrl = model.VideoUrl?.Trim(),
                Difficulty = model.Difficulty,
                Equipment = model.Equipment?.Trim(),
                ExerciseCategoryId = model.ExerciseCategoryId,
                IsActive = model.IsActive,
                IsDeleted = false,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = UserId,
            };

            _db.Exercises.Add(exercise);

            foreach (var mgId in model.SelectedMuscleGroupIds.Distinct())
            {
                _db.ExerciseMuscleGroups.Add(new ExerciseMuscleGroup
                {
                    ExerciseId = exerciseId,
                    MuscleGroupId = mgId,
                    IsPrimary = true,
                });
            }

            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Exercise \"{exercise.ExerciseName}\" created successfully.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        // GET /Exercises/Edit/id
        public async Task<IActionResult> Edit(Guid id)
        {
            var e = await _db.Exercises
                .Include(x => x.ExerciseMuscleGroups)
                .FirstOrDefaultAsync(x => x.ExerciseId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (e == null) return NotFound();

            var vm = new ExerciseFormViewModel
            {
                ExerciseId = e.ExerciseId,
                ExerciseName = e.ExerciseName,
                Description = e.Description,
                Instructions = e.Instructions,
                ExerciseCategoryId = e.ExerciseCategoryId,
                Difficulty = e.Difficulty,
                Equipment = e.Equipment,
                VideoUrl = e.VideoUrl,
                ExistingPhotoUrl = e.PhotoUrl,
                IsActive = e.IsActive,
                SelectedMuscleGroupIds = e.ExerciseMuscleGroups.Select(mg => mg.MuscleGroupId).ToList(),
                Categories = await GetCategoriesAsync(),
                AllMuscleGroups = await GetMuscleGroupsAsync(),
            };

            ViewData["Title"] = "Exercise Library";
            ViewData["Subtitle"] = "Edit Exercise";
            return View("CreateEdit", vm);
        }

        // POST /Exercises/Edit/id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, ExerciseFormViewModel model)
        {
            var e = await _db.Exercises
                .Include(x => x.ExerciseMuscleGroups)
                .FirstOrDefaultAsync(x => x.ExerciseId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (e == null) return NotFound();

            if (!ModelState.IsValid)
            {
                model.ExistingPhotoUrl = e.PhotoUrl;
                model.Categories = await GetCategoriesAsync();
                model.AllMuscleGroups = await GetMuscleGroupsAsync();
                ViewData["Title"] = "Exercise Library";
                ViewData["Subtitle"] = "Edit Exercise";
                return View("CreateEdit", model);
            }

            if (model.Photo != null)
            {
                DeletePhoto(e.PhotoUrl);
                e.PhotoUrl = await SavePhotoAsync(model.Photo, id);
            }

            e.ExerciseName = model.ExerciseName.Trim();
            e.Description = model.Description?.Trim();
            e.Instructions = model.Instructions?.Trim();
            e.ExerciseCategoryId = model.ExerciseCategoryId;
            e.Difficulty = model.Difficulty;
            e.Equipment = model.Equipment?.Trim();
            e.VideoUrl = model.VideoUrl?.Trim();
            e.IsActive = model.IsActive;
            e.UpdatedAtUtc = DateTime.UtcNow;
            e.UpdatedByUserId = UserId;

            _db.ExerciseMuscleGroups.RemoveRange(e.ExerciseMuscleGroups);
            foreach (var mgId in model.SelectedMuscleGroupIds.Distinct())
            {
                _db.ExerciseMuscleGroups.Add(new ExerciseMuscleGroup
                {
                    ExerciseId = id,
                    MuscleGroupId = mgId,
                    IsPrimary = true,
                });
            }

            await _db.SaveChangesAsync();

            TempData["Toast"] = $"\"{e.ExerciseName}\" updated.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        // POST /Exercises/Delete/id (soft delete)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var e = await _db.Exercises
                .FirstOrDefaultAsync(x => x.ExerciseId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (e == null) return NotFound();

            e.IsDeleted = true;
            e.IsActive = false;
            e.UpdatedAtUtc = DateTime.UtcNow;
            e.UpdatedByUserId = UserId;

            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Exercise \"{e.ExerciseName}\" removed.";
            TempData["ToastType"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        // ── Helpers ──────────────────────────────────

        private async Task<List<ExerciseCategoryDropdownItem>> GetCategoriesAsync() =>
            await _db.ExerciseCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.ExerciseCategoryId)
                .Select(c => new ExerciseCategoryDropdownItem
                {
                    ExerciseCategoryId = c.ExerciseCategoryId,
                    CategoryName = c.CategoryName,
                    IconClass = c.IconClass,
                })
                .ToListAsync();

        private async Task<List<MuscleGroupItem>> GetMuscleGroupsAsync() =>
            await _db.MuscleGroups
                .OrderBy(m => m.BodyPart)
                .ThenBy(m => m.MuscleGroupName)
                .Select(m => new MuscleGroupItem
                {
                    MuscleGroupId = m.MuscleGroupId,
                    MuscleGroupName = m.MuscleGroupName,
                    BodyPart = m.BodyPart,
                })
                .ToListAsync();

        private async Task<string?> SavePhotoAsync(IFormFile? file, Guid exerciseId)
        {
            if (file == null || file.Length == 0) return null;

            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext)) return null;

            var folder = Path.Combine(_env.WebRootPath, "uploads", "exercises");
            Directory.CreateDirectory(folder);

            var fileName = $"{exerciseId}{ext}";
            var filePath = Path.Combine(folder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/exercises/{fileName}";
        }

        private void DeletePhoto(string? photoUrl)
        {
            if (string.IsNullOrEmpty(photoUrl)) return;
            var filePath = Path.Combine(_env.WebRootPath, photoUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }

        // GET /Exercises/QuickMediaEdit
        public async Task<IActionResult> QuickMediaEdit()
        {
            var exercises = await _db.Exercises
                .Where(e => e.TenantId == TenantId && !e.IsDeleted)
                .OrderBy(e => e.ExerciseCategoryId)
                .ThenBy(e => e.ExerciseName)
                .Select(e => new QuickMediaEditItem
                {
                    ExerciseId   = e.ExerciseId,
                    ExerciseName = e.ExerciseName,
                    CategoryName = e.Category.CategoryName,
                    CategoryId   = e.ExerciseCategoryId,
                    VideoUrl     = e.VideoUrl,
                    PhotoUrl     = e.PhotoUrl,
                })
                .ToListAsync();

            ViewData["Title"] = "Exercise Library";
            return View(exercises);
        }

        // POST /Exercises/QuickMediaEdit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickMediaEdit(List<QuickMediaEditItem> exercises)
        {
            if (exercises == null || exercises.Count == 0)
                return RedirectToAction(nameof(Index));

            var ids = exercises.Select(e => e.ExerciseId).ToList();
            var dbMap = await _db.Exercises
                .Where(e => ids.Contains(e.ExerciseId) && e.TenantId == TenantId && !e.IsDeleted)
                .ToDictionaryAsync(e => e.ExerciseId);

            int updated = 0;
            foreach (var item in exercises)
            {
                if (!dbMap.TryGetValue(item.ExerciseId, out var ex)) continue;
                var newUrl = string.IsNullOrWhiteSpace(item.VideoUrl) ? null : item.VideoUrl.Trim();
                if (ex.VideoUrl == newUrl) continue;
                ex.VideoUrl         = newUrl;
                ex.UpdatedAtUtc     = DateTime.UtcNow;
                ex.UpdatedByUserId  = UserId;
                updated++;
            }

            await _db.SaveChangesAsync();

            TempData["Toast"]     = updated > 0
                ? $"{updated} exercise{(updated == 1 ? "" : "s")} updated."
                : "No changes detected.";
            TempData["ToastType"] = updated > 0 ? "success" : "warning";
            return RedirectToAction(nameof(Index));
        }

        // POST /Exercises/SeedDefault
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SeedDefault()
        {
            var existingNames = (await _db.Exercises
                .Where(e => e.TenantId == TenantId && !e.IsDeleted)
                .Select(e => e.ExerciseName.ToLower())
                .ToListAsync())
                .ToHashSet();

            var defaults = GetDefaultExercises();
            var now      = DateTime.UtcNow;
            var tenantId = TenantId;
            var userId   = UserId;
            int added    = 0;

            foreach (var ex in defaults)
            {
                if (existingNames.Contains(ex.Name.ToLower())) continue;

                var id = Guid.NewGuid();
                _db.Exercises.Add(new Exercise
                {
                    ExerciseId          = id,
                    TenantId            = tenantId,
                    ExerciseName        = ex.Name,
                    Description         = ex.Description,
                    Difficulty          = ex.Difficulty,
                    Equipment           = ex.Equipment,
                    ExerciseCategoryId  = ex.CategoryId,
                    IsActive            = true,
                    IsDeleted           = false,
                    CreatedAtUtc        = now,
                    CreatedByUserId     = userId,
                });

                foreach (var mgId in ex.MuscleGroupIds)
                    _db.ExerciseMuscleGroups.Add(new ExerciseMuscleGroup
                        { ExerciseId = id, MuscleGroupId = mgId, IsPrimary = true });

                added++;
            }

            await _db.SaveChangesAsync();

            TempData["Toast"]     = added > 0
                ? $"{added} exercises loaded into your library successfully."
                : "All default exercises already exist in your library.";
            TempData["ToastType"] = added > 0 ? "success" : "warning";
            return RedirectToAction(nameof(Index));
        }

        // ── Default exercise seed data ────────────────
        // MuscleGroup IDs: 1=Chest 2=Back 3=Shoulders 4=Biceps 5=Triceps 6=Forearms
        //                  7=Abs 8=Obliques 9=Lower Back 10=Quads 11=Hamstrings
        //                  12=Glutes 13=Calves 14=Hip Flexors 15=Full Body
        // Category IDs:    2=Cardio 5=Core&Balance 8=Chest 9=Back 10=Shoulders 11=Arms 12=Legs
        private record ExerciseSeed(string Name, string? Description, int CategoryId, byte Difficulty, string? Equipment, int[] MuscleGroupIds);

        private static IReadOnlyList<ExerciseSeed> GetDefaultExercises() =>
        [
            // ── CHEST ─────────────────────────────────────────────────────────────────
            new("Barbell Flat Bench Press",        "The classic horizontal push. Lies flat, press bar from lower chest to full extension.",                                  8, 2, "Barbell",       [1, 5]),
            new("Barbell Incline Bench Press",     "Press at a 30-45° incline to shift emphasis to the upper chest and front delts.",                                       8, 2, "Barbell",       [1, 3]),
            new("Barbell Decline Bench Press",     "Press on a downward-angled bench to target the lower chest.",                                                           8, 2, "Barbell",       [1, 5]),
            new("Dumbbell Flat Bench Press",       "Independent dumbbells allow a greater range of motion and help correct left-right strength imbalances.",                 8, 1, "Dumbbell",      [1, 5]),
            new("Dumbbell Incline Bench Press",    "Incline dumbbell press for upper chest development with increased range of motion.",                                     8, 1, "Dumbbell",      [1, 3]),
            new("Dumbbell Decline Bench Press",    "Decline dumbbell press targeting the lower pec fibers.",                                                                8, 1, "Dumbbell",      [1, 5]),
            new("Dumbbell Chest Fly (Flat)",       "Wide arc motion that isolates the pec major. Keep a slight bend in the elbows throughout.",                             8, 1, "Dumbbell",      [1]),
            new("Dumbbell Chest Fly (Incline)",    "Incline fly to stretch and contract the upper chest through a full arc.",                                               8, 1, "Dumbbell",      [1]),
            new("Cable Chest Fly (Low-to-High)",   "Cables maintain tension throughout. Low-to-high angle emphasises upper pec fibers.",                                    8, 1, "Cable Machine", [1]),
            new("Cable Chest Fly (High-to-Low)",   "High-to-low cable fly targets the lower and mid chest with constant tension.",                                          8, 1, "Cable Machine", [1]),
            new("Pec Deck / Chest Fly Machine",    "Machine chest fly. Great for beginners and for finishing sets with constant tension.",                                  8, 1, "Machine",       [1]),
            new("Chest Press Machine",             "Machine bench press variant. Easy to load and unload; good for beginners and high-rep work.",                           8, 1, "Machine",       [1, 5]),
            new("Push-Up",                         "Foundational bodyweight press. Strengthens chest, shoulders, and triceps with no equipment.",                           8, 1, "Bodyweight",    [1, 3, 5]),
            new("Wide-Grip Push-Up",               "Wider hand placement increases chest activation and reduces tricep involvement.",                                       8, 1, "Bodyweight",    [1]),
            new("Diamond Push-Up",                 "Hands form a diamond beneath the chest. Shifts focus heavily to the triceps and inner chest.",                          8, 1, "Bodyweight",    [5, 1]),
            new("Dips (Chest Variation)",          "Lean forward during dips to shift load from triceps to the lower chest.",                                              8, 2, "Bodyweight",    [1, 5]),
            new("Cable Crossover",                 "Cross cables in front of the chest for a strong peak contraction. Adjustable height changes emphasis.",                 8, 2, "Cable Machine", [1]),

            // ── BACK ──────────────────────────────────────────────────────────────────
            new("Conventional Deadlift",           "King of compound lifts. Pulls a loaded bar from the floor engaging the entire posterior chain.",                        9, 3, "Barbell",       [2, 11, 12, 9]),
            new("Romanian Deadlift (RDL)",         "Hip-hinge with soft knees. Primary hamstring and glute builder; great for posterior chain development.",                9, 2, "Barbell",       [11, 12, 9]),
            new("Sumo Deadlift",                   "Wide stance pulls the bar from the floor. Greater glute and inner thigh involvement than conventional.",                9, 3, "Barbell",       [12, 2, 11]),
            new("Rack Pull",                       "Partial deadlift from knee height. Allows heavier loads and emphasises the upper back and traps.",                      9, 3, "Barbell",       [2, 9]),
            new("Pull-Up (Overhand / Pronated)",   "Overhand pull-up. Wide grip targets the lats and develops a V-taper. Hardest back bodyweight movement.",               9, 2, "Pull-up Bar",   [2, 4]),
            new("Chin-Up (Underhand / Supinated)", "Underhand pull-up. Greater bicep involvement than pronated pull-up. Slightly easier for beginners.",                   9, 2, "Pull-up Bar",   [2, 4]),
            new("Neutral-Grip Pull-Up",            "Palms facing each other. Easiest pull-up variation, comfortable on the wrists and shoulders.",                         9, 2, "Pull-up Bar",   [2, 4]),
            new("Lat Pulldown (Wide Grip)",        "Pull a bar down to the upper chest. Wide grip maximises lat activation. Great for building width.",                    9, 1, "Cable Machine", [2, 4]),
            new("Lat Pulldown (Close Grip)",       "Neutral-grip close-pull. Slightly more range of motion and increased bicep engagement.",                               9, 1, "Cable Machine", [2, 4]),
            new("Seated Cable Row (Wide Grip)",    "Row a cable bar to the abdomen with a wide grip for upper back thickness.",                                            9, 1, "Cable Machine", [2, 3]),
            new("Seated Cable Row (Close Grip)",   "Row with a V-handle to the lower abdomen. Strong mid-back builder with good range of motion.",                        9, 1, "Cable Machine", [2, 4]),
            new("Bent-Over Barbell Row (Overhand)","Hip-hinged row. Overhand grip targets the upper back and rear delts.",                                                 9, 2, "Barbell",       [2, 3]),
            new("Bent-Over Barbell Row (Underhand)","Underhand grip row increases bicep activation and allows more weight than overhand.",                                  9, 2, "Barbell",       [2, 4]),
            new("T-Bar Row",                       "Landmine or T-bar machine row. Allows heavy loading with a neutral spine.",                                            9, 2, "Barbell",       [2, 4]),
            new("Single-Arm Dumbbell Row",         "Unilateral row with one knee on a bench. Excellent for back thickness and correcting imbalances.",                     9, 1, "Dumbbell",      [2, 4]),
            new("Chest-Supported Dumbbell Row",    "Row face-down on an incline bench. Removes lower-back stress so the back muscles do all the work.",                   9, 1, "Dumbbell",      [2]),
            new("Pendlay Row",                     "Barbell row where the bar resets on the floor each rep. Explosive pull builds power and upper back strength.",          9, 3, "Barbell",       [2, 9]),
            new("Face Pull",                       "Pull a rope attachment to face height. Targets rear delts and external rotators. Essential for shoulder health.",      9, 1, "Cable Machine", [3, 2]),
            new("Straight-Arm Cable Pulldown",     "Pushes the cable down from overhead with straight arms, isolating the lats through a long range of motion.",           9, 1, "Cable Machine", [2]),
            new("Hyperextension (Back Extension)", "Hinge over a back extension bench to train the lower back, glutes and hamstrings.",                                   9, 1, "Machine",       [9, 12, 11]),

            // ── SHOULDERS ────────────────────────────────────────────────────────────
            new("Barbell Overhead Press (Standing)","Standing barbell press overhead. Full-body tension makes it the premier shoulder compound movement.",                  10, 2, "Barbell",      [3, 5]),
            new("Barbell Overhead Press (Seated)",  "Seated version removes leg drive, isolating the shoulders and triceps.",                                              10, 2, "Barbell",      [3, 5]),
            new("Dumbbell Shoulder Press (Seated)", "Each arm presses independently, improving balance and allowing greater range of motion than a barbell.",              10, 1, "Dumbbell",     [3, 5]),
            new("Dumbbell Shoulder Press (Standing)","Standing dumbbell press adds core stability demand to the shoulder movement.",                                        10, 1, "Dumbbell",     [3, 5]),
            new("Arnold Press",                     "Dumbbell press with a rotating motion. Hits all three delt heads through a larger arc than standard press.",          10, 2, "Dumbbell",     [3]),
            new("Lateral Raise (Dumbbell)",         "Raise dumbbells to the side to isolate the medial (middle) delt. The primary shoulder width builder.",                10, 1, "Dumbbell",     [3]),
            new("Lateral Raise (Cable)",            "Cable lateral raise. Constant tension throughout; easier to keep strict form than dumbbells.",                        10, 1, "Cable Machine",[3]),
            new("Lateral Raise (Machine)",          "Machine lateral raise for beginners or high-rep sets. Locked movement pattern minimises cheating.",                   10, 1, "Machine",      [3]),
            new("Front Raise (Dumbbell)",           "Raise dumbbell to shoulder height in front of the body. Targets the anterior (front) delt.",                         10, 1, "Dumbbell",     [3]),
            new("Front Raise (Barbell)",            "Barbell front raise allows heavier load. Both arms move together, challenging core stability.",                        10, 1, "Barbell",      [3]),
            new("Rear Delt Fly (Dumbbell)",         "Bent-over fly isolates the posterior deltoid. Crucial for posture and shoulder balance.",                             10, 1, "Dumbbell",     [3, 2]),
            new("Rear Delt Fly (Cable)",            "Face-down or bent-over cable fly for the rear delt. Constant tension improves mind-muscle connection.",               10, 1, "Cable Machine",[3]),
            new("Rear Delt Machine Fly",            "Machine reverse fly. Easy to set up and great for isolation and high-rep rear delt work.",                            10, 1, "Machine",      [3]),
            new("Upright Row (Barbell)",            "Pull bar up along the body to chin height. Targets the medial delt and traps. Keep grip shoulder-width.",             10, 2, "Barbell",      [3, 2]),
            new("Upright Row (Dumbbell)",           "Dumbbell upright row. Slightly more wrist freedom reduces impingement risk vs. barbell.",                             10, 2, "Dumbbell",     [3, 2]),
            new("Barbell Shrugs",                   "Elevate the shoulders straight up to target the upper trapezius. Use straps for heavier sets.",                       10, 1, "Barbell",      [3]),
            new("Dumbbell Shrugs",                  "Dumbbell version of the shrug. Allows full range of motion at the sides of the body.",                                10, 1, "Dumbbell",     [3]),
            new("Cable Upright Row",                "Low pulley upright row provides constant tension and a smoother pull path than free weights.",                        10, 2, "Cable Machine",[3, 2]),

            // ── ARMS ─────────────────────────────────────────────────────────────────
            // Biceps
            new("Barbell Bicep Curl",              "Standard two-handed curl. Allows the most weight of any curl variation; great for overall mass.",                      11, 1, "Barbell",       [4]),
            new("EZ-Bar Curl",                     "Angled bar reduces wrist stress while maintaining strong bicep activation.",                                           11, 1, "Barbell",       [4]),
            new("Dumbbell Bicep Curl (Standing)",  "Alternate or simultaneous dumbbell curl. Supinate the wrist at the top for peak contraction.",                        11, 1, "Dumbbell",      [4]),
            new("Dumbbell Bicep Curl (Seated)",    "Seated curl eliminates body swing, forcing strict bicep-only movement.",                                              11, 1, "Dumbbell",      [4]),
            new("Hammer Curl",                     "Neutral grip curl. Works the brachialis and brachioradialis as well as the bicep; builds arm thickness.",              11, 1, "Dumbbell",      [4, 6]),
            new("Preacher Curl (Barbell)",         "Arm braced on a preacher bench removes cheating. Great for isolating the bicep peak.",                                11, 1, "Barbell",       [4]),
            new("Preacher Curl (Dumbbell)",        "Unilateral preacher curl allows full supination for a better bicep contraction.",                                     11, 1, "Dumbbell",      [4]),
            new("Concentration Curl",              "Seated isolation curl braced against the inner thigh. Maximises the bicep peak contraction.",                         11, 1, "Dumbbell",      [4]),
            new("Cable Curl (Straight Bar)",       "Low-pulley cable curl with constant tension throughout the range of motion.",                                         11, 1, "Cable Machine", [4]),
            new("Cable Curl (Rope)",               "Rope cable curl allows wrists to rotate, varying the stimulus on the bicep.",                                         11, 1, "Cable Machine", [4]),
            new("Incline Dumbbell Curl",           "Curl on an incline bench to stretch the long head of the bicep at the bottom for a stronger peak.",                   11, 1, "Dumbbell",      [4]),
            new("Spider Curl",                     "Curl face-down on an incline bench. Removes all momentum and isolates the bicep fully.",                              11, 2, "Dumbbell",      [4]),
            new("Reverse Curl",                    "Pronated grip curl targeting the brachialis and brachioradialis for complete forearm and bicep development.",           11, 1, "Barbell",       [4, 6]),
            // Triceps
            new("Tricep Pushdown (Straight Bar)",  "Push a straight bar down from a high pulley. Core tricep isolation exercise for all three heads.",                    11, 1, "Cable Machine", [5]),
            new("Tricep Pushdown (Rope)",          "Rope attachment allows wrists to pronate at the bottom for better lateral head contraction.",                         11, 1, "Cable Machine", [5]),
            new("Tricep Pushdown (V-Bar)",         "V-bar gives a comfortable neutral grip. Good alternative to straight bar for those with wrist issues.",               11, 1, "Cable Machine", [5]),
            new("Skull Crusher (EZ-Bar)",          "Lower bar to forehead and extend. Primary mass-builder for the long head of the tricep.",                             11, 2, "Barbell",       [5]),
            new("Overhead Tricep Extension (Dumbbell)","Extend a dumbbell overhead to fully stretch the long head of the tricep.",                                         11, 1, "Dumbbell",      [5]),
            new("Overhead Tricep Extension (Cable)","Rope overhead extension. Constant cable tension through a full range of motion.",                                     11, 1, "Cable Machine", [5]),
            new("Close-Grip Bench Press",          "Narrowed grip on the barbell bench press shifts load from chest to triceps.",                                         11, 2, "Barbell",       [5, 1]),
            new("Tricep Bench Dips",               "Dips on a flat bench with feet on the floor. Beginner-friendly tricep exercise requiring no equipment.",              11, 1, "Bodyweight",    [5]),
            new("Dips (Tricep Variation)",         "Upright torso keeps the load on triceps rather than chest. A fundamental pressing movement.",                         11, 2, "Bodyweight",    [5, 1]),
            new("Tricep Kickback",                 "Extend the dumbbell behind the body while hinged over. Best for the lateral head at full extension.",                  11, 1, "Dumbbell",      [5]),

            // ── LEGS ──────────────────────────────────────────────────────────────────
            new("Barbell Back Squat",              "The king of leg exercises. Bar on upper traps; squat to parallel or below. Full leg and glute developer.",             12, 2, "Barbell",       [10, 12, 11]),
            new("Barbell Front Squat",             "Bar rests on front delts. More upright torso shifts emphasis to quads and requires greater mobility.",                12, 3, "Barbell",       [10, 12]),
            new("Goblet Squat",                    "Hold a dumbbell or kettlebell at the chest. Great teaching tool for squat mechanics and core engagement.",             12, 1, "Kettlebell",    [10, 12]),
            new("Leg Press",                       "Push a weighted sled through a fixed track. Allows very heavy quad and glute training with a protected back.",         12, 1, "Machine",       [10, 12, 11]),
            new("Hack Squat Machine",              "Squat in a fixed-track machine. High quad activation with less lower-back stress than a barbell squat.",              12, 2, "Machine",       [10, 12]),
            new("Lying Leg Curl",                  "Curl the weight up while lying face-down on a machine. The primary isolated hamstring builder.",                      12, 1, "Machine",       [11]),
            new("Seated Leg Curl",                 "Curl from a seated position. Places the hamstring in a different angle to lying leg curl.",                            12, 1, "Machine",       [11]),
            new("Leg Extension",                   "Extend the lower leg against resistance. Isolates the quadriceps; excellent for knee strengthening.",                 12, 1, "Machine",       [10]),
            new("Walking Lunge",                   "Step forward into a deep lunge alternating legs. Great for quads, glutes and balance.",                               12, 1, "Bodyweight",    [10, 12]),
            new("Reverse Lunge",                   "Step backward into a lunge. Less knee stress than a forward lunge; emphasises glutes more.",                          12, 1, "Bodyweight",    [10, 12]),
            new("Bulgarian Split Squat",           "Rear foot elevated on a bench. The hardest single-leg movement; unmatched for quad and glute hypertrophy.",           12, 2, "Dumbbell",      [10, 12]),
            new("Step-Up",                         "Step onto a raised platform. Functional single-leg movement targeting quads and glutes.",                              12, 1, "Dumbbell",      [10, 12]),
            new("Barbell Hip Thrust",              "Drive hips up against a barbell resting across the lap. The best glute isolation exercise.",                          12, 2, "Barbell",       [12, 11]),
            new("Dumbbell Hip Thrust",             "Hip thrust with dumbbells. Good for beginners before progressing to barbell.",                                        12, 1, "Dumbbell",      [12, 11]),
            new("Glute Bridge",                    "Bodyweight or weighted bridge lying on the floor. Activates the glutes and is a safe entry point.",                   12, 1, "Bodyweight",    [12, 11]),
            new("Sumo Squat",                      "Wide-stance squat with toes pointing outward. Greater inner thigh and glute activation than standard squat.",         12, 1, "Dumbbell",      [12, 10]),
            new("Standing Calf Raise",             "Rise on to tiptoes against a loaded machine. Primary gastrocnemius (outer calf) builder.",                            12, 1, "Machine",       [13]),
            new("Seated Calf Raise",               "Calf raise seated at 90°. Targets the soleus (inner calf) more than standing raises.",                               12, 1, "Machine",       [13]),
            new("Leg Press Calf Raise",            "Push up on toes using the leg press machine. High load calf training without balance demand.",                        12, 1, "Machine",       [13]),
            new("Leg Abduction Machine",           "Push knees outward against pads. Isolates the glute medius and hip abductors.",                                      12, 1, "Machine",       [12]),
            new("Leg Adduction Machine",           "Pull knees together against pads. Works the inner thigh (adductor) muscles.",                                         12, 1, "Machine",       [14]),
            new("Box Jump",                        "Explosive jump onto a plyo box. Develops lower-body power, fast-twitch fibres and conditioning.",                     12, 2, "Bodyweight",    [10, 12]),
            new("Sled Push",                       "Drive a weighted sled across the floor. Full-body power and conditioning movement that spares the joints.",            12, 3, "Machine",       [15]),

            // ── CORE (Category 5) ─────────────────────────────────────────────────────
            new("Plank",                           "Hold a straight-body position on forearms. Builds deep core stability and total-body tension.",                        5,  1, "Bodyweight",    [7]),
            new("Side Plank",                      "Lateral plank on one forearm. Targets the obliques and hip abductors.",                                               5,  1, "Bodyweight",    [7, 8]),
            new("Crunch",                          "Curl the upper torso off the floor. The foundational ab isolation exercise.",                                          5,  1, "Bodyweight",    [7]),
            new("Bicycle Crunch",                  "Alternate elbow-to-knee crunch with a twisting motion. High oblique and rectus abdominis activation.",                5,  1, "Bodyweight",    [7, 8]),
            new("Cable Crunch",                    "Kneel and crunch down against a rope cable. Allows progressive overload on the abs.",                                 5,  1, "Cable Machine", [7]),
            new("Hanging Leg Raise",               "Hang from a bar and raise legs to parallel or above. Demanding lower-ab and hip-flexor exercise.",                    5,  2, "Pull-up Bar",   [7, 14]),
            new("Lying Leg Raise",                 "Raise straight legs from the floor while lying on your back. Targets the lower abdominals.",                          5,  1, "Bodyweight",    [7, 14]),
            new("Russian Twist",                   "Rotate the torso side-to-side while seated and leaning back. Oblique and rotational strength builder.",               5,  1, "Bodyweight",    [7, 8]),
            new("Ab Wheel Rollout",                "Roll the wheel forward from kneeling to a prone stretch and back. Very demanding core stability drill.",               5,  2, "Bodyweight",    [7]),
            new("Mountain Climbers",               "Drive knees alternately toward the chest in a plank position. Core stability, hip flexors and cardio combined.",       5,  1, "Bodyweight",    [7, 14]),
            new("Dead Bug",                        "Lying core drill: extend opposite arm and leg while pressing back into the floor. Safe and highly effective.",         5,  1, "Bodyweight",    [7]),
            new("Hollow Hold",                     "Press the lower back flat and hold an extended hollowed position. Gymnastic core discipline.",                         5,  2, "Bodyweight",    [7]),
            new("Dragon Flag",                     "Full-body lever movement on a bench. One of the hardest bodyweight ab exercises.",                                     5,  3, "Bodyweight",    [7]),
            new("Pallof Press",                    "Anti-rotation press from a cable or band. Trains the core to resist rotation rather than create it.",                  5,  2, "Cable Machine", [7, 8]),
            new("Decline Crunch",                  "Crunch on a decline bench. Greater range of motion and resistance than floor crunches.",                               5,  1, "Bodyweight",    [7]),
            new("V-Up",                            "Simultaneously raise arms and legs to meet in the middle. Dynamic full ab contraction.",                               5,  2, "Bodyweight",    [7]),

            // ── CARDIO (Category 2) ───────────────────────────────────────────────────
            new("Treadmill Running",               "Controlled-pace running on a treadmill. Foundational aerobic conditioning.",                                           2,  1, "Machine",       [15]),
            new("Stationary Bike",                 "Low-impact cardio. Easy on the joints and great for long steady-state sessions.",                                      2,  1, "Machine",       [15]),
            new("Rowing Machine",                  "Full-body cardio engaging legs, back, and arms. High calorie burn with low impact.",                                   2,  2, "Machine",       [15]),
            new("Elliptical Trainer",              "Smooth, low-impact cardio mimicking running without joint stress.",                                                    2,  1, "Machine",       [15]),
            new("Jump Rope",                       "Skipping rope drills. Great for coordination, conditioning and warming up.",                                           2,  1, "Bodyweight",    [15]),
            new("Stair Climber",                   "Climbing machine for lower-body cardio with a high glute and quad demand.",                                            2,  1, "Machine",       [12, 10]),
            new("Battle Ropes",                    "Alternating or simultaneous wave slams. Intense upper-body cardio and grip strength.",                                 2,  2, "Bodyweight",    [15]),
            new("Burpees",                         "Drop to a push-up, jump up and repeat. Full-body conditioning and fat-burning movement.",                              2,  2, "Bodyweight",    [15]),
            new("Assault Bike (Air Bike)",         "Fan-resistance bike with moving handles. Brutal full-body cardio; used in HIIT and CrossFit.",                        2,  2, "Machine",       [15]),
            new("Sprint Intervals",                "Short maximal-effort sprints with rest periods. Develops speed, power and anaerobic fitness.",                         2,  2, "Bodyweight",    [15]),
        ];
    }
}
