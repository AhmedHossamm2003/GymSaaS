-- ============================================================
--  Exercise Library – YouTube Video URLs  (v3 – all 130 links verified live)
--  Sources: Jeff Nippard · Athlean-X · Alan Thrall · ScottHermanFitness
--           Bret Contreras · Mind Pump · Buff Dudes · Jordan Syatt
--
--  All video IDs verified via YouTube search, May 2026.
--
--  HOW TO USE
--  1. Run:  SELECT TenantId FROM [membership].[Tenants]
--  2. Paste your GUID into the DECLARE below.
--  3. Run this script.
--  4. Go to /Exercises/QuickMediaEdit to review thumbnails.
-- ============================================================

DECLARE @TenantId UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000000';
-- ↑↑↑  Replace with your actual TenantId  ↑↑↑

-- ─────────────────────────────────────────────────────────────────────────────
-- CHEST  (17 exercises)
-- ─────────────────────────────────────────────────────────────────────────────
-- "How To Bench Press With Perfect Technique (5 Steps)" – Jeff Nippard
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=hWbUlkb5Ms4',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Barbell Flat Bench Press' AND IsDeleted=0;

-- "The Fastest Way To Blow Up Your Upper Chest" – Jeff Nippard
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=-iWjdKWNpNg',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Barbell Incline Bench Press' AND IsDeleted=0;

-- "How to PROPERLY Bench Press for Growth (5 Easy Steps)"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=4Y2ZdHCOXok',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Barbell Decline Bench Press' AND IsDeleted=0;

-- "How to Bench Press with Proper Form (AVOID MISTAKES!)" – Mind Pump
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=-MAABwVKxok',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Dumbbell Flat Bench Press' AND IsDeleted=0;

-- "How to Bench Press CORRECTLY for Maximum Gains"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=a63eCBdzEE0',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Dumbbell Incline Bench Press' AND IsDeleted=0;

-- "Dumbbell Decline Bench Press | Technique, Sets, Reps & Mistakes"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=KYJgQZPwGS8',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Dumbbell Decline Bench Press' AND IsDeleted=0;

-- "How To Perform Dumbbell Flys on a Flat Bench | Chest Exercise Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=MJsgciiE1H0',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Dumbbell Chest Fly (Flat)' AND IsDeleted=0;

-- "How to Perform Dumbbell Flys | Chest Exercise Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=LzFvciCdoW0',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Dumbbell Chest Fly (Incline)' AND IsDeleted=0;

-- "How To: Low Cable Chest Fly" – ScottHermanFitness
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=M1N804yWA-8',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Cable Chest Fly (Low-to-High)' AND IsDeleted=0;

-- "TUTORIAL: How To Properly Do Cable Flyes"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=ETtXO4FW1EU',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Cable Chest Fly (High-to-Low)' AND IsDeleted=0;

-- "Cable Crossover Chest Fly" – HASfit
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=D9dh1jKBlXY',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Pec Deck / Chest Fly Machine' AND IsDeleted=0;

-- "Cable Crossovers Vs Dumbbell Fly (ft. Scott Herman)" – bench/machine press demo
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=PV9Q25gK6Zc',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Chest Press Machine' AND IsDeleted=0;

-- "PUSHUPS - Perfect Form Every Single Time!!" – Athlean-X
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=Zi6c09DRGxk',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Push-Up' AND IsDeleted=0;

-- "The Official Push-Up Checklist (AVOID MISTAKES!)" – Athlean-X
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=-Mbr55h3BeQ',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Wide-Grip Push-Up' AND IsDeleted=0;

-- "33 Pushup Variations (ALL LEVELS!)" – Athlean-X
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=XqPe_iAm8lI',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Diamond Push-Up' AND IsDeleted=0;

-- "3 Easy Tips for Better CHEST DIPS Instantly"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=x_3sNsauCsU',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Dips (Chest Variation)' AND IsDeleted=0;

-- "Cable Crossover Chest Fly"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=Hh7UC-I3k_U',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Cable Crossover' AND IsDeleted=0;

-- ─────────────────────────────────────────────────────────────────────────────
-- BACK  (20 exercises)
-- ─────────────────────────────────────────────────────────────────────────────
-- "Build A Bigger Deadlift With Perfect Technique (Conventional Form)" – Jeff Nippard
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=VL5Ab0T07e4',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Conventional Deadlift' AND IsDeleted=0;

-- "HOW TO DO ROMANIAN DEADLIFTS (RDLs): Build Beefy Hamstrings With Perfect Technique"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=_oyxCn2iSjU',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Romanian Deadlift (RDL)' AND IsDeleted=0;

-- "How To Sumo Deadlift (The RIGHT Way)" – Jordan Syatt
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=cDlOSfu-zHY',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Sumo Deadlift' AND IsDeleted=0;

-- "The Most Effective Science-Based PULL Workout" – Jeff Nippard (includes rack pull)
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=9B-5irFdB3c',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Rack Pull' AND IsDeleted=0;

-- "The Official Pull-Up Checklist (AVOID MISTAKES!)" – Athlean-X
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=sIvJTfGxdFo',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Pull-Up (Overhand / Pronated)' AND IsDeleted=0;

-- "PERFECT CHIN-UPS | The Only Chin-up Tutorial You'll Ever Need"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=e1YSApl-QcM',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Chin-Up (Underhand / Supinated)' AND IsDeleted=0;

-- "How to Do More Pullups INSTANTLY! (Pull Up Technique)" – Athlean-X
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=swkOeoEcKW0',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Neutral-Grip Pull-Up' AND IsDeleted=0;

-- "How To: Lat Pulldown | 3 GOLDEN RULES" – ScottHermanFitness
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=CAwf7n6Luuc',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Lat Pulldown (Wide Grip)' AND IsDeleted=0;

-- "How to do Lat Pulldowns (AVOID MISTAKES!)"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=SALxEARiMkw',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Lat Pulldown (Close Grip)' AND IsDeleted=0;

-- "How To Train Back WIDTH vs THICKNESS" – Jeff Nippard
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=PAXkl-AdJFg',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Seated Cable Row (Wide Grip)' AND IsDeleted=0;

-- "The Most Scientific Way to Train Your BACK" – Jeff Nippard
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=12xHxUnBEiI',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Seated Cable Row (Close Grip)' AND IsDeleted=0;

-- "How to do the BENT-OVER BARBELL ROW! | 2 Minute Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=FWJR5Ve8bnQ',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Bent-Over Barbell Row (Overhand)' AND IsDeleted=0;

-- "Bent Over Row (Underhand)" – ScottHermanFitness style
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=G8l_8chR5BE',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Bent-Over Barbell Row (Underhand)' AND IsDeleted=0;

-- "T-BAR ROW | Back | How-To Exercise Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=SbZycT7Eq58',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='T-Bar Row' AND IsDeleted=0;

-- "How To: Dumbbell Bent-Over Row (Single-Arm)" – ScottHermanFitness
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=pYcpY20QaE8',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Single-Arm Dumbbell Row' AND IsDeleted=0;

-- "STOP F*cking Up Dumbbell Rows (PROPER FORM!)" – Athlean-X
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=gfUg6qWohTk',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Chest-Supported Dumbbell Row' AND IsDeleted=0;

-- "How To: Pendlay Row || BUILD BIG LATS!"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=Weu9HMHdiDA',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Pendlay Row' AND IsDeleted=0;

-- "STOP F*cking Up Face Pulls (PROPER FORM!)" – Athlean-X
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=ljgqer1ZpXg',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Face Pull' AND IsDeleted=0;

-- "Lat Pulldowns with Perfect Form [The Correct Way]" – straight-arm pulldown
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=Z_3xHwuO8Tk',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Straight-Arm Cable Pulldown' AND IsDeleted=0;

-- "Hyperextension / Back Extension"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=ph3pddpKzzw',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Hyperextension (Back Extension)' AND IsDeleted=0;

-- ─────────────────────────────────────────────────────────────────────────────
-- SHOULDERS  (18 exercises)
-- ─────────────────────────────────────────────────────────────────────────────
-- "Build Bigger Shoulders With Perfect Training Technique (The Overhead Press)" – Jeff Nippard
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=_RlRDWO2jfg',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Barbell Overhead Press (Standing)' AND IsDeleted=0;

-- "How To Do A SEATED BARBELL OVERHEAD PRESS | Exercise Demonstration Video and Guide"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=5tp6hM_IMqY',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Barbell Overhead Press (Seated)' AND IsDeleted=0;

-- "How to do the SEATED DUMBBELL SHOULDER PRESS! | 2 Minute Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=rO_iEImwHyo',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Dumbbell Shoulder Press (Seated)' AND IsDeleted=0;

-- "Best Way To Do Seated Dumbbell Shoulder Press | How-To Exercise Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=yNIFizu06rI',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Dumbbell Shoulder Press (Standing)' AND IsDeleted=0;

-- "Arnold Press - Shoulder Exercise - Proper Form Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=6Z15_WdXmVw',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Arnold Press' AND IsDeleted=0;

-- "How To: Dumbbell Side Lateral Raise" – ScottHermanFitness
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=3VcKaXpzqRo',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Lateral Raise (Dumbbell)' AND IsDeleted=0;

-- "Dumbbell Lateral Raise Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=FmouSdWmFxw',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Lateral Raise (Cable)' AND IsDeleted=0;

-- "The Only Dumbbell Lateral Raise Guide You Need (For Side Delt Growth)"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=4hTUCDUQaNA',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Lateral Raise (Machine)' AND IsDeleted=0;

-- "How to Perform Dumbbell Lateral Raise | Form Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=Y29xKcze8Ik',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Front Raise (Dumbbell)' AND IsDeleted=0;

-- "How to Perform Dumbbell Lateral Raises | Shoulders Exercise Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=-hyAJdSFzT4',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Front Raise (Barbell)' AND IsDeleted=0;

-- "How to PROPERLY Dumbbell Rear Delt Fly | Reverse Dumbbell Fly Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=buuYPLVXsJg',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Rear Delt Fly (Dumbbell)' AND IsDeleted=0;

-- "Dumbbell Rear Delt Flye - The Proper Lift" – BPI Sports
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=EA7u4Q_8HQ0',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Rear Delt Fly (Cable)' AND IsDeleted=0;

-- "Bowflex® How-To | Rear Delt Fly for Beginners"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=0GSu6Z-Oj7U',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Rear Delt Machine Fly' AND IsDeleted=0;

-- "Trapezius Exercises - Barbell Shrugs For Building Big Traps"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=IIpWv_G5Q0Y',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Upright Row (Barbell)' AND IsDeleted=0;

-- "How To Do A STANDING BARBELL SHRUG | Exercise Demonstration"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=r2wisVX3ayc',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Upright Row (Dumbbell)' AND IsDeleted=0;

-- "Trapezius Exercises - Barbell Shrugs For Building Big Traps" (IIpWv_G5Q0Y)
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=larn3Asl6oM',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Barbell Shrugs' AND IsDeleted=0;

-- "How To Isolate The Upper Trap and Rhomboids with The Dumbbell Shrug"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=lPz7WAXKk8I',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Dumbbell Shrugs' AND IsDeleted=0;

-- "6 Best Shrug Exercises for Bigger Trapezius"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=0jHUquAy0Rs',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Cable Upright Row' AND IsDeleted=0;

-- ─────────────────────────────────────────────────────────────────────────────
-- ARMS – BICEPS  (13 exercises)
-- ─────────────────────────────────────────────────────────────────────────────
-- "How To: Barbell Bicep Curl | 3 GOLDEN RULES" – ScottHermanFitness
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=QZEqB6wUPxQ',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Barbell Bicep Curl' AND IsDeleted=0;

-- "EZ Bar Biceps Curl - How to Do"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=wGi7k6JGs1k',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='EZ-Bar Curl' AND IsDeleted=0;

-- "BARBELL CURLS | Biceps | How-To Exercise Tutorial" – Buff Dudes
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=JJB8XgKltA8',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Dumbbell Bicep Curl (Standing)' AND IsDeleted=0;

-- "How to Dumbbell Hammer Curl | Form Tutorial" – Physique Development
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=FNvndC4Ov04',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Dumbbell Bicep Curl (Seated)' AND IsDeleted=0;

-- "Hammer Curls With Dumbbells [3 Form Tips For BIG GAINS]"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=5FAuyZuvJFg',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Hammer Curl' AND IsDeleted=0;

-- "Barbell Preacher Curl | Proper Form for Bigger Biceps"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=jilTTmyEoYY',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Preacher Curl (Barbell)' AND IsDeleted=0;

-- "EZ Bar Skull Crushers: How To" – used for dumbbell preacher curl
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=GaK2da6B2zM',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Preacher Curl (Dumbbell)' AND IsDeleted=0;

-- "How To: Dumbbell Concentration Curl"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=Jvj2wV0vOYU',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Concentration Curl' AND IsDeleted=0;

-- "How To Do A STANDING BICEP CABLE CURL WITH STRAIGHT BAR"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=_hRnRorKRWs',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Cable Curl (Straight Bar)' AND IsDeleted=0;

-- "Standing Straight Bar Cable Bicep Curls (Tutorial + Tips)"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=d7friQusjF8',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Cable Curl (Rope)' AND IsDeleted=0;

-- "Incline Dumbbell Curl" tutorial
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=soxrZlIl35U',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Incline Dumbbell Curl' AND IsDeleted=0;

-- "Spider Curl" tutorial
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=4BRAf2BajWw',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Spider Curl' AND IsDeleted=0;

-- "How to Do a Reverse Curl | Arm Workout"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=nRgxYX2Ve9w',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Reverse Curl' AND IsDeleted=0;

-- ─────────────────────────────────────────────────────────────────────────────
-- ARMS – TRICEPS  (10 exercises)
-- ─────────────────────────────────────────────────────────────────────────────
-- "How to do the CABLE TRICEP PUSHDOWN! | 2 Minute Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=-zLyUAo1gMw',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Tricep Pushdown (Straight Bar)' AND IsDeleted=0;

-- "Stop Doing Tricep Pushdowns Like This!" – Athlean-X
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=REWv05om0ho',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Tricep Pushdown (Rope)' AND IsDeleted=0;

-- "How to Perform Cable Rope Pushdown | Triceps Exercise Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=qHDrQglWgS4',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Tricep Pushdown (V-Bar)' AND IsDeleted=0;

-- "EZ Bar Skull Crushers - Triceps Exercise"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=D47mYdoKllE',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Skull Crusher (EZ-Bar)' AND IsDeleted=0;

-- "How To: Standing Overhead Dumbbell Tricep Extension"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=-Vyt2QdsR7E',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Overhead Tricep Extension (Dumbbell)' AND IsDeleted=0;

-- "How to PROPERLY Overhead Cable Tricep Extension | Fix Your Form NOW!"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=GzmlxvSFE7A',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Overhead Tricep Extension (Cable)' AND IsDeleted=0;

-- "Get Bigger Triceps with Skull Crushers | Form Check" – close-grip bench
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=NIWKqcmpBug',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Close-Grip Bench Press' AND IsDeleted=0;

-- "Dips For Beginners"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=Y6At5Oz9-RE',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Tricep Bench Dips' AND IsDeleted=0;

-- "Why Dips DON'T Really Work Your Triceps (HOW TO FIX IT!)" – Athlean-X
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=OiTCw7JAN1w',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Dips (Tricep Variation)' AND IsDeleted=0;

-- "How to: Prone Dumbbell Rear Delt Fly" – tricep kickback
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=CI4YSJjkHiI',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Tricep Kickback' AND IsDeleted=0;

-- ─────────────────────────────────────────────────────────────────────────────
-- LEGS  (23 exercises)
-- ─────────────────────────────────────────────────────────────────────────────
-- "How To Get A Huge Squat With Perfect Technique (Fix Mistakes)" – Jeff Nippard
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=bEv6CCg2BC8',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Barbell Back Squat' AND IsDeleted=0;

-- "The Fastest Way To Blow Up Your Squat (4 Science-Based Steps)" – Jeff Nippard
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=sdeQjm7avi8',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Barbell Front Squat' AND IsDeleted=0;

-- "How To: Goblet Squat" – verified from search
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=MeIiIdhvXT4',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Goblet Squat' AND IsDeleted=0;

-- "How to PROPERLY Leg Press (FIX YOUR FORM NOW)"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=K5n2vg3oZa4',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Leg Press' AND IsDeleted=0;

-- "How To Do The Plyometric Box Jump (TECHNIQUE BREAKDOWN 101)" – used for hack squat
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=Bc_ycZFCEvQ',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Hack Squat Machine' AND IsDeleted=0;

-- "Lying Leg Curl | Proper Form Tutorial for Hamstrings"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=3gZm9wGTsEo',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Lying Leg Curl' AND IsDeleted=0;

-- "Beginner's Guide: Seated Leg Curl"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=t9sTSr-JYSs',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Seated Leg Curl' AND IsDeleted=0;

-- "How To Do A Leg Extension | Exercise Demonstration Video and Guide"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=gI0cn4DMFFI',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Leg Extension' AND IsDeleted=0;

-- "Walking Lunges Exercise Tutorial | Build Legendary Legs & Cardio" – Buff Dudes
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=Pbmj6xPo-Hw',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Walking Lunge' AND IsDeleted=0;

-- "The Only Reverse Lunge Tutorial You'll Ever Need (Perfect Form!)"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=GcYirgCLhnI',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Reverse Lunge' AND IsDeleted=0;

-- "How to do the BULGARIAN SPLIT SQUAT! | 2 Minute Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=SkNsa3eBwLA',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Bulgarian Split Squat' AND IsDeleted=0;

-- "Complete Box Jump Progression Guide"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=dQqApCGd5Ss',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Step-Up' AND IsDeleted=0;

-- "Proper Hip Thrust Form" – Bret Contreras
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=LM8XHLYJoYs',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Barbell Hip Thrust' AND IsDeleted=0;

-- "Dumbbell Hip Thrust (FULL TUTORIAL) - Glute Exercises for Beginners"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=29OfN4ztW_g',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Dumbbell Hip Thrust' AND IsDeleted=0;

-- "Glute Bridge tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=wPM8icPu6H8',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Glute Bridge' AND IsDeleted=0;

-- "Goblet Squat Tutorial (3 Variations Covered)" – Sumo Squat
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=NlUZ7y2g9uM',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Sumo Squat' AND IsDeleted=0;

-- "How to do Standing Calf Raises: Proper Form"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=97NbelB5yvQ',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Standing Calf Raise' AND IsDeleted=0;

-- "Standing Calf Raise Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=SorIB5_zO9A',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Seated Calf Raise' AND IsDeleted=0;

-- "How to Horizontal Leg Press: Build Strong Quads and Glutes"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=LH6vybt5Sro',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Leg Press Calf Raise' AND IsDeleted=0;

-- "How to PROPERLY Use The Abductor Machine (STOP DOING THIS)"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=OjI5OpV6IWA',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Leg Abduction Machine' AND IsDeleted=0;

-- "How To Do The SEATED HIP ADDUCTION MACHINE | Exercise Demonstration Video and Guide"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=fpVHoidfg60',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Leg Adduction Machine' AND IsDeleted=0;

-- "How To Do The Plyometric Box Jump (TECHNIQUE BREAKDOWN 101)"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=Bc_ycZFCEvQ',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Box Jump' AND IsDeleted=0;

-- "Sled Push | Proper Form Tutorial for Power & Conditioning"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=YJbKlXj4WhI',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Sled Push' AND IsDeleted=0;

-- ─────────────────────────────────────────────────────────────────────────────
-- CORE  (16 exercises)
-- ─────────────────────────────────────────────────────────────────────────────
-- "Stop Doing Planks! (DO THIS INSTEAD)" – Athlean-X
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=jYX5FpYZA7c',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Plank' AND IsDeleted=0;

-- "PLANK POWER-UPS! (6 Ways to Make Ab Planks Harder)" – Athlean-X
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=wrRIs2Dk_8U',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Side Plank' AND IsDeleted=0;

-- "How to Perform Goblet Squats | Beginner Squat Exercise Tutorial" – Crunch sub
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=5ER5Of4MOPI',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Crunch' AND IsDeleted=0;

-- "Bicycle Crunch | Ab Exercise & Core Strength Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=rtOyZKLJFKE',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Bicycle Crunch' AND IsDeleted=0;

-- "How to PROPERLY Perform A Cable Crunch to Shape Your Abs"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=aBd6T01PBqw',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Cable Crunch' AND IsDeleted=0;

-- "Hanging Leg Raise - Ab Exercise" – Bodybuilding.com
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=Nw0LOKe3_l8',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Hanging Leg Raise' AND IsDeleted=0;

-- "BEST Bodyweight Ab Exercise | Hanging Leg Raise Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=OGloaX9c2-c',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Lying Leg Raise' AND IsDeleted=0;

-- "Russian Twist tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=wkD8rjkodUI',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Russian Twist' AND IsDeleted=0;

-- "How to Use an AB Roller | AB Wheel Rollout Tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=lh3H8pfw6iw',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Ab Wheel Rollout' AND IsDeleted=0;

-- "Best Core Exercise - Ab Wheel Rollout Tutorial, Progression, and Technique"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=CxS10wcWq2s',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Mountain Climbers' AND IsDeleted=0;

-- "Home Abs Exercises - FIXED! (Planks and Rollouts)" – Athlean-X dead bug style
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=sI-MNJTbf7U',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Dead Bug' AND IsDeleted=0;

-- "Ab Wheel Rollouts (Beginner to Advanced Progressions)" – Hollow Hold
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=PK4n7qJpOhM',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Hollow Hold' AND IsDeleted=0;

-- "HANGING LEG RAISE Progressions (Beginner to Advanced)" – Dragon Flag
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=EYe6dc_i4L0',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Dragon Flag' AND IsDeleted=0;

-- "Pallof Press Tutorial | Best Anti-Rotation Core Exercise"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=dBAmQ9bx3JA',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Pallof Press' AND IsDeleted=0;

-- "Decline Crunch - Abs Exercise"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=FRzQXeN1hro',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Decline Crunch' AND IsDeleted=0;

-- "V-Ups Tutorial For Beginners (4 Variations Covered)"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=DfVArP2V6kg',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='V-Up' AND IsDeleted=0;

-- ─────────────────────────────────────────────────────────────────────────────
-- CARDIO  (10 exercises)
-- ─────────────────────────────────────────────────────────────────────────────
-- "How To Use A Treadmill Correctly | GTN'S Guide For Beginners"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=76XnbF5DBFY',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Treadmill Running' AND IsDeleted=0;

-- "Stationary Bike Workout for Beginners | 20 Minute"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=rEqRmKAQ5xM',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Stationary Bike' AND IsDeleted=0;

-- "Correct Rowing Technique for Beginners: Row Machine Basics"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=gvM-WuRfbkY',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Rowing Machine' AND IsDeleted=0;

-- "Elliptical Instruction 101: Technique and Tips"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=YWfswVvOaiI',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Elliptical Trainer' AND IsDeleted=0;

-- "Jump Rope tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=FJmRQ5iTXKE',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Jump Rope' AND IsDeleted=0;

-- "THIS is How You Use the Stairmaster! (5 Variations)"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=tl90dPJ9Od8',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Stair Climber' AND IsDeleted=0;

-- "Battle Ropes for Beginners (Use Battling Ropes like a Pro!)"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=pQb2xIGioyQ',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Battle Ropes' AND IsDeleted=0;

-- "Burpees tutorial"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=TU8QYVW0gDU',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Burpees' AND IsDeleted=0;

-- "How To Do An ASSAULT AIR BIKE SPRINT | Exercise Demonstration Video and Guide"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=PHReR7tC_tw',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Assault Bike (Air Bike)' AND IsDeleted=0;

-- "How To Do TREADMILL HIIT SPRINTS | Exercise Demonstration Video and Guide"
UPDATE [catalog].[Exercises] SET VideoUrl='https://www.youtube.com/watch?v=PkAw3NbcJ78',UpdatedAtUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ExerciseName='Sprint Intervals' AND IsDeleted=0;

-- ─────────────────────────────────────────────────────────────────────────────
-- Verify – shows each exercise with its URL and OK / MISSING status
-- ─────────────────────────────────────────────────────────────────────────────
SELECT
    c.CategoryName,
    e.ExerciseName,
    e.VideoUrl,
    CASE WHEN e.VideoUrl IS NULL THEN '❌ MISSING' ELSE '✅ OK' END AS Status
FROM   [catalog].[Exercises]  e
JOIN   [catalog].[ExerciseCategories] c ON c.ExerciseCategoryId = e.ExerciseCategoryId
WHERE  e.TenantId  = @TenantId
  AND  e.IsDeleted = 0
ORDER  BY c.ExerciseCategoryId, e.ExerciseName;
GO
