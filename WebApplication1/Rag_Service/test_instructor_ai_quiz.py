"""
test_instructor_ai_quiz.py
Verifies end-to-end AI PDF-to-Quiz generation through ASP.NET MVC backend.
"""

import requests
import json
from html.parser import HTMLParser

BASE_URL = "http://localhost:57794"
session = requests.Session()

print("=" * 70)
print("TESTING INSTRUCTOR AI PDF-TO-QUIZ WORKFLOW")
print("=" * 70)

# Step 1: Login as Instructor Dr. Vikram Sen (Instructor for Course 2 & 6)
login_page = session.get(f"{BASE_URL}/Account/Login")
print(f"[Step 1] Visiting Login Page -> Status {login_page.status_code}")

class TokenParser(HTMLParser):
    def __init__(self):
        super().__init__()
        self.token = None
    def handle_starttag(self, tag, attrs):
        attrs_dict = dict(attrs)
        if tag == "input" and attrs_dict.get("name") == "__RequestVerificationToken":
            self.token = attrs_dict.get("value")

parser = TokenParser()
parser.feed(login_page.text)

login_payload = {
    "Email": "asharma@lms.com",
    "Password": "Pass@123",
    "RememberMe": "false",
    "__RequestVerificationToken": parser.token
}

login_res = session.post(f"{BASE_URL}/Account/Login", data=login_payload, allow_redirects=True)
print(f"[Step 2] Login Response -> Status {login_res.status_code}, URL: {login_res.url}")

# Step 3: Manage Quiz Page for Course 2
quiz_page = session.get(f"{BASE_URL}/Instructor/ManageQuiz/2")
print(f"[Step 3] ManageQuiz/2 Page -> Status {quiz_page.status_code}")
has_ai_btn = "Generate Quiz from PDF (AI)" in quiz_page.text
print(f"  ManageQuiz contains 'Generate Quiz from PDF (AI)' button: {has_ai_btn}")

# Step 4: Call /Instructor/GenerateQuizFromPdf with Course2 PDF
pdf_path = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "Uploads", "CourseMaterials", "Course2_ASPNET_MVC5_Enterprise_Architecture.pdf"))
with open(pdf_path, "rb") as f:
    files = {"quizPdf": ("Course2_ASPNET_MVC5_Enterprise_Architecture.pdf", f, "application/pdf")}
    data = {
        "courseId": 2,
        "lessonNumber": 1,
        "numQuestions": 3,
        "topic": "Idempotency and Anti-Forgery CSRF Tokens"
    }
    gen_res = session.post(f"{BASE_URL}/Instructor/GenerateQuizFromPdf", files=files, data=data)
    print(f"\n[Step 4] GenerateQuizFromPdf Response -> Status {gen_res.status_code}")
    try:
        gen_json = gen_res.json()
        print(f"  Success: {gen_json.get('success')}")
        print(f"  Total Questions Generated: {gen_json.get('total_questions')}")
        questions = gen_json.get("questions", [])
        for i, q in enumerate(questions, 1):
            print(f"  Q{i}: {q.get('question_text')}")
            print(f"     A) {q.get('option_a')}")
            print(f"     B) {q.get('option_b')}")
            print(f"     C) {q.get('option_c')}")
            print(f"     D) {q.get('option_d')}")
            print(f"     Correct: {q.get('correct_option')} | Expl: {q.get('explanation')}")

        if questions:
            # Step 5: Test Saving the Generated Questions via /Instructor/SaveGeneratedQuestions
            save_payload = {
                "courseId": 2,
                "lessonNumber": 1,
                "questions": [
                    {
                        "QuestionText": q["question_text"],
                        "OptionA": q["option_a"],
                        "OptionB": q["option_b"],
                        "OptionC": q["option_c"],
                        "OptionD": q["option_d"],
                        "CorrectOption": q["correct_option"]
                    }
                    for q in questions[:2]  # Save 2 of them
                ]
            }
            save_res = session.post(
                f"{BASE_URL}/Instructor/SaveGeneratedQuestions",
                json=save_payload
            )
            print(f"\n[Step 5] SaveGeneratedQuestions Response -> Status {save_res.status_code}")
            print(f"  Result: {save_res.json()}")
    except Exception as e:
        print(f"  Parse error: {e}, text: {gen_res.text[:300]}")

print("\n" + "=" * 70)
print("TEST COMPLETED!")
print("=" * 70)
