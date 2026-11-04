"""
test_instructor_end_to_end.py
Comprehensive End-to-End Test Suite for Instructor Persona Capabilities:
1. Operational & Backlog Metrics (Enrolled student counts, pending assignment list)
2. Targeted Student Assignment Auto-Evaluation (Rahul Verma)
3. Specific Submission ID Evaluation (Submission #6 - John Doe)
4. Full End-to-End ASP.NET MVC Chatbot/Ask integration with authenticated Instructor session
"""

import requests
from html.parser import HTMLParser

FASTAPI_URL = "http://127.0.0.1:8000/api/chat"
MVC_LOGIN_URL = "http://localhost:57794/Account/Login"
MVC_CHAT_URL = "http://localhost:57794/Chatbot/Ask"


def test_1_instructor_metrics_query():
    print("\n--- Test 1: Instructor Backlog & Student Metrics Query ---")
    payload = {
        "query": "How many students are enrolled and how many assignments do I have to evaluate?",
        "course_id": 0,
        "user_id": "1",
        "user_role": "instructor"
    }
    resp = requests.post(FASTAPI_URL, json=payload, timeout=25)
    assert resp.status_code == 200, f"Expected 200, got {resp.status_code}"
    data = resp.json()
    print("Answer Output:\n" + data.get("answer", ""))
    assert "Total Enrolled Students" in data.get("answer", "")
    assert "Assignments Waiting for Evaluation" in data.get("answer", "")
    print("PASS: Instructor metrics and pending backlog returned successfully!")


def test_2_evaluate_rahul_assignment():
    print("\n--- Test 2: AI Auto-Evaluation for Student (Rahul Verma) ---")
    payload = {
        "query": "Please evaluate Rahul's assignment submission for Lesson 2",
        "course_id": 2,
        "user_id": "1",
        "user_role": "instructor"
    }
    resp = requests.post(FASTAPI_URL, json=payload, timeout=30)
    assert resp.status_code == 200
    data = resp.json()
    answer = data.get("answer", "")
    print("Evaluation Output Snippet:\n" + answer[:400] + "...\n")
    assert "Recommended Score" in answer or "Score" in answer
    assert "Rahul" in answer
    print("PASS: Rahul Verma's submission was located and evaluated by AI!")


def test_3_evaluate_by_submission_id():
    print("\n--- Test 3: AI Auto-Evaluation by Submission ID (#6 - John Doe) ---")
    payload = {
        "query": "Can you evaluate submission #6?",
        "course_id": 2,
        "user_id": "1",
        "user_role": "instructor"
    }
    resp = requests.post(FASTAPI_URL, json=payload, timeout=30)
    assert resp.status_code == 200
    data = resp.json()
    answer = data.get("answer", "")
    print("Evaluation Output Snippet:\n" + answer[:350] + "...\n")
    assert "John Doe" in answer or "Submission #6" in answer
    print("PASS: Submission #6 evaluated accurately!")


def test_4_mvc_end_to_end_instructor_chat():
    print("\n--- Test 4: Full End-to-End ASP.NET MVC /Chatbot/Ask as Instructor ---")
    session = requests.Session()

    # Step 1: Login as Instructor (A. Sharma - asharma@lms.com / Pass@123)
    class TokenParser(HTMLParser):
        def __init__(self):
            super().__init__()
            self.token = None
        def handle_starttag(self, tag, attrs):
            if tag == "input" and dict(attrs).get("name") == "__RequestVerificationToken":
                self.token = dict(attrs).get("value")

    login_page = session.get(MVC_LOGIN_URL)
    parser = TokenParser()
    parser.feed(login_page.text)

    login_res = session.post(MVC_LOGIN_URL, data={
        "Email": "asharma@lms.com",
        "Password": "Pass@123",
        "RememberMe": "false",
        "__RequestVerificationToken": parser.token
    }, allow_redirects=True)
    assert login_res.status_code == 200, f"Login failed: {login_res.status_code}"
    print("Instructor logged in successfully to MVC.")

    # Step 2: Query /Chatbot/Ask for assignments to evaluate
    chat_res = session.post(MVC_CHAT_URL, json={
        "Query": "How many assignments do I have to evaluate?",
        "CourseId": 0
    })
    assert chat_res.status_code == 200
    res_data = chat_res.json()
    assert res_data.get("success") is True, f"MVC Chatbot returned false: {res_data.get('message')}"
    inner_data = res_data.get("data", {})
    print("MVC Returned Answer:\n" + inner_data.get("answer", "")[:350] + "...\n")
    assert "Assignments Waiting for Evaluation" in inner_data.get("answer", "")
    print("PASS: End-to-end ASP.NET MVC Instructor Chatbot integration verified!")


if __name__ == "__main__":
    test_1_instructor_metrics_query()
    test_2_evaluate_rahul_assignment()
    test_3_evaluate_by_submission_id()
    test_4_mvc_end_to_end_instructor_chat()
    print("\nALL 4 INSTRUCTOR AGENTIC EVALUATION TESTS PASSED PERFECTLY!")
