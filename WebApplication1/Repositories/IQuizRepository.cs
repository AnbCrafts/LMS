using System.Collections.Generic;
using WebApplication1.Models;

namespace WebApplication1.Repositories
{
    public interface IQuizRepository
    {
        IEnumerable<QuizQuestion> GetQuizByCourseAndLesson(int courseId, int lessonNumber);
        IEnumerable<QuizQuestion> GetAllQuestionsForCourse(int courseId);
        QuizQuestion GetQuestionById(int questionId);
        OperationResult CreateQuestion(QuizQuestion question);
        OperationResult UpdateQuestion(QuizQuestion question);
        OperationResult DeleteQuestion(int questionId);
    }
}
