document.addEventListener("dragstart", (event) => {
    const target = event.target instanceof Element
        ? event.target.closest("[data-category-id]")
        : null;

    if (!target || !event.dataTransfer) return;

    event.dataTransfer.effectAllowed = "move";
    event.dataTransfer.setData("text/plain", target.dataset.categoryId);
});