---
layout: default
title: Authors
nav_order: 9
---

<style>
.author-card {
  display: flex;
  align-items: center;
  gap: 1.25rem;
  margin-bottom: 2rem;
}
.author-avatar {
  width: 96px;
  height: 96px;
  border-radius: 50%;
  border: 1px solid #ddd;
  flex-shrink: 0;
}
.author-info h1 {
  margin: 0 0 0.35rem;
  font-size: 1.1rem;
  border: none;
  padding: 0;
}
.author-links {
  display: flex;
  gap: 1rem;
  font-size: 0.85rem;
}
.gh-readme-section h2 {
  margin-top: 0;
}
#gh-readme-loading {
  color: #999;
  font-size: 0.85rem;
}
</style>

<div class="author-card">
  <img class="author-avatar"
       src="https://github.com/Tauheed-Elahee.png"
       alt="Tauheed Elahee">
  <div class="author-info">
    <h1>Tauheed Elahee</h1>
    <div class="author-links">
      <a href="https://github.com/Tauheed-Elahee">github.com/Tauheed-Elahee</a>
    </div>
  </div>
</div>

<div class="gh-readme-section">
  <div id="gh-readme-loading">Loading profile…</div>
  <div id="gh-readme"></div>
</div>

<script src="https://cdn.jsdelivr.net/npm/marked/marked.min.js"></script>
<script>
fetch('https://raw.githubusercontent.com/Tauheed-Elahee/Tauheed-Elahee/main/README.md')
  .then(function(r) { return r.text(); })
  .then(function(md) {
    document.getElementById('gh-readme-loading').style.display = 'none';
    document.getElementById('gh-readme').innerHTML = marked.parse(md);
  })
  .catch(function() {
    document.getElementById('gh-readme-loading').style.display = 'none';
  });
</script>
