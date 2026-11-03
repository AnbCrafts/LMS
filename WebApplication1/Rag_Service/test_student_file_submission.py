"""
test_student_file_submission.py
Verifies student file upload for practical assignment submission in CoursesController.
"""

import requests
import io
from html.parser import HTMLParser

BASE_URL = "http://localhost:57794"
session = requests.Session()

print("=" * 70)
print("TESTING STUDENT FILE UPLOAD ASSIGNMENT SUBMISSION")
print("=" * 70)

# Step 1: Login as Student (student@lms.com)
login_page = session.get(f"{BASE_URL}/Account/Login")
class TokenParser(HTMLParser):
    def __init__(self):
        super().__init__()
        self.token = None
    def handle_starttag(self, tag, attrs):
        if tag == "input" and dict(attrs).get("name") == "__RequestVerificationToken":
            self.token = dict(attrs).get("value")

parser = TokenParser()
parser.feed(login_page.text)

login_res = session.post(f"{BASE_URL}/Account/Login", data={
    "Email": "priya.sharma@lms.com",
    "Password": "Pass@123",
    "RememberMe": "false",
    "__RequestVerificationToken": parser.token
}, allow_redirects=True)
print(f"[Step 1] Login as priya.sharma@lms.com -> Status {login_res.status_code}, URL: {login_res.url}")

# Step 2: Visit Learn page for Course 2, Lesson 2
learn_page = session.get(f"{BASE_URL}/Courses/Learn/2?lesson=2")
print(f"[Step 2] Visit Learn/2?lesson=2 -> Status {learn_page.status_code}")

learn_parser = TokenParser()
learn_parser.feed(learn_page.text)

# Step 3: Submit assignment with an attached PDF
fake_pdf = b"%PDF-1.4\n1 0 obj\n<< /Title (Student Solution) >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n"
files = {
    "submissionFile": ("Rahul_MVC_Controller_Assignment.pdf", io.BytesIO(fake_pdf), "application/pdf")
}
data = {
    "__RequestVerificationToken": learn_parser.token,
    "courseId": 2,
    "lessonNumber": 2,
    "submissionTitle": "Lesson 2: Core Components Practical Task",
    "submissionText": "Implemented custom AuthorizeAttribute and Repository pattern with unit tests in NUnit.",
    "submissionFileUrl": "https://github.com/rahulverma/lms-mvc-architecture"
}

sub_res = session.post(f"{BASE_URL}/Courses/SubmitAssignment", files=files, data=data, allow_redirects=True)
print(f"[Step 3] SubmitAssignment Response -> Status {sub_res.status_code}, Final URL: {sub_res.url}")

# Step 4: Verify Learn page now shows submitted document
has_sub_badge = "Assignment Finalized" in sub_res.text or "Submitted on" in sub_res.text
has_attached_doc = "Attached Solution Document" in sub_res.text or "Download File" in sub_res.text
print(f"[Step 4] Contains 'Assignment Finalized': {has_sub_badge}")
print(f"         Contains 'Attached Solution Document / Download': {has_attached_doc}")

print("\n" + "=" * 70)
print("TEST COMPLETED!")
print("=" * 70)
