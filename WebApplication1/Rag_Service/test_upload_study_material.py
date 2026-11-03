"""
test_upload_study_material.py
Verifies uploading a study material file (PDF) to a course module via InstructorController.SaveModule.
"""

import requests
import io
from html.parser import HTMLParser

BASE_URL = "http://localhost:57794"
session = requests.Session()

print("=" * 70)
print("TESTING STUDY MATERIAL UPLOAD (PDF / VIDEO / URL)")
print("=" * 70)

# Step 1: Login as Instructor (A. Sharma)
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
    "Email": "asharma@lms.com",
    "Password": "Pass@123",
    "RememberMe": "false",
    "__RequestVerificationToken": parser.token
}, allow_redirects=True)
print(f"[Step 1] Login as asharma@lms.com -> Status {login_res.status_code}")

# Step 2: Visit Modules page for Course 1
mod_page = session.get(f"{BASE_URL}/Instructor/Modules/1")
print(f"[Step 2] Visit Instructor/Modules/1 -> Status {mod_page.status_code}")

mod_parser = TokenParser()
mod_parser.feed(mod_page.text)

# Step 3: Save module with attached PDF file
test_pdf_content = b"%PDF-1.4\n1 0 obj\n<< /Title (React 18 Architecture Slides) >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n"
files = {
    "materialFile": ("React18_Lesson1_Lecture_Slides.pdf", io.BytesIO(test_pdf_content), "application/pdf")
}
data = {
    "__RequestVerificationToken": mod_parser.token,
    "CourseId": 1,
    "ModuleId": 1,
    "ModuleNumber": 1,
    "Title": "Course Introduction & Setup",
    "DurationMinutes": 25,
    "Description": "Setting up tools, understanding foundational architecture, and project organization.",
    "ContentMarkdown": "Welcome to React 18! Review the lecture slides attached above.",
    "VideoOrResourceUrl": "",
    "AssignmentPrompt": "Setup a fresh Vite + React 18 project with TypeScript."
}

save_res = session.post(f"{BASE_URL}/Instructor/SaveModule", files=files, data=data, allow_redirects=True)
print(f"[Step 3] SaveModule Response -> Status {save_res.status_code}, Final URL: {save_res.url}")

# Step 4: Login as enrolled Student (student@lms.com) and verify Learn/1?lesson=1 renders PDF player
student_session = requests.Session()
s_login_page = student_session.get(f"{BASE_URL}/Account/Login")
s_parser = TokenParser()
s_parser.feed(s_login_page.text)

student_session.post(f"{BASE_URL}/Account/Login", data={
    "Email": "student@lms.com",
    "Password": "Pass@123",
    "RememberMe": "false",
    "__RequestVerificationToken": s_parser.token
})

learn_res = student_session.get(f"{BASE_URL}/Courses/Learn/1?lesson=1")
print(f"[Step 4] Student Learn/1?lesson=1 Page -> Status {learn_res.status_code}")
has_pdf_player = "Official Lesson Slides & Lecture PDF" in learn_res.text or "Open / Download PDF" in learn_res.text
print(f"         Contains Embedded PDF / Study Material Player: {has_pdf_player}")

print("\n" + "=" * 70)
print("TEST COMPLETED!")
print("=" * 70)
