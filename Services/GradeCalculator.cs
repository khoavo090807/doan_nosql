using doan_cuoiky_nosql.Models;
using MongoDB.Bson;

namespace doan_cuoiky_nosql.Services;

/// <summary>Shared grading rules for score completion and official GPA inclusion.</summary>
public static class GradeCalculator
{
    // The current grading scheme uses these weights. Practice is omitted only when
    // the course has no practical hours, then the remaining weights are normalized.
    private const double AttendanceWeight = 0.1;
    private const double MidtermWeight = 0.2;
    private const double PracticeWeight = 0.2;
    private const double FinalWeight = 0.5;

    public static bool UsesPractice(MonHoc? course) => course == null || course.ThucHanh > 0;

    public static bool HasAnyScore(DiemThanhPhan? scores) =>
        scores?.ChuyenCan.HasValue == true || scores?.GiuaKy.HasValue == true ||
        scores?.ThucHanh.HasValue == true || scores?.CuoiKy.HasValue == true;

    public static bool IsComplete(DiemThanhPhan? scores, bool usesPractice) =>
        scores?.ChuyenCan.HasValue == true && scores.GiuaKy.HasValue &&
        scores.CuoiKy.HasValue && (!usesPractice || scores.ThucHanh.HasValue);

    public static double? CalculateFinal(DiemThanhPhan? scores, bool usesPractice)
    {
        if (!IsComplete(scores, usesPractice) || scores == null) return null;

        var weighted = scores.ChuyenCan!.Value * AttendanceWeight +
            scores.GiuaKy!.Value * MidtermWeight + scores.CuoiKy!.Value * FinalWeight;
        var totalWeight = AttendanceWeight + MidtermWeight + FinalWeight;
        if (usesPractice)
        {
            weighted += scores.ThucHanh!.Value * PracticeWeight;
            totalWeight += PracticeWeight;
        }

        return Math.Round(weighted / totalWeight, 1);
    }

    public static string? LetterGrade(double? score) => score switch
    {
        null => null,
        >= 9.0 => "A+",
        >= 8.5 => "A",
        >= 8.0 => "B+",
        >= 7.0 => "B",
        >= 6.5 => "C+",
        >= 5.5 => "C",
        >= 5.0 => "D+",
        >= 4.0 => "D",
        _ => "F"
    };

    public static string ScoreStatus(DiemThanhPhan? scores) =>
        HasAnyScore(scores) ? "Chưa hoàn tất điểm" : "Chưa có điểm";

    public static List<DiemCLO> BuildCloResults(MonHoc? course, double? finalScore) =>
        (course?.CLO ?? new List<CLOMonHoc>()).Select(clo => new DiemCLO
        {
            MaCLO = clo.MaCLO,
            DiemDat = finalScore,
            KetQua = finalScore.HasValue
                ? (finalScore.Value >= 5 ? "Đạt" : "Không đạt")
                : "Chưa đánh giá"
        }).ToList();

    /// <summary>Mongo expression for fully entered courses, retaining numeric zero as a valid value.</summary>
    public static BsonDocument CompletedCourseCondition(IReadOnlyCollection<string> noPracticeCourseIds)
    {
        static BsonDocument HasValue(string field) => new("$ne", new BsonArray
        {
            new BsonDocument("$ifNull", new BsonArray { field, BsonNull.Value }),
            BsonNull.Value
        });

        return new BsonDocument("$and", new BsonArray
        {
            HasValue("$$b.DiemTongKet"),
            HasValue("$$b.DiemThanhPhan.ChuyenCan"),
            HasValue("$$b.DiemThanhPhan.GiuaKy"),
            HasValue("$$b.DiemThanhPhan.CuoiKy"),
            new BsonDocument("$or", new BsonArray
            {
                HasValue("$$b.DiemThanhPhan.ThucHanh"),
                new BsonDocument("$in", new BsonArray
                {
                    "$$b.MaMon",
                    new BsonArray(noPracticeCourseIds)
                })
            })
        });
    }
}
