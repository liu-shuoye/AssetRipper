// For enabling and disabling descriptions based on the selected option in a select element
document.addEventListener('DOMContentLoaded', function () {
	// Get all select elements on the page
	var selects = document.querySelectorAll('select');

	// Iterate through each select element
	selects.forEach(function (select) {
		// Add event listener to the select element to update the descriptions
		select.addEventListener('change', function () {
			for (let i = 0; i < select.options.length; i++) {
				var option = select.options[i];
				var descriptionId = option.getAttribute('option-description');
				var description = document.getElementById(descriptionId);
				if (description) {
					if (i == select.selectedIndex) {
						//Enable description
						description.classList.remove('disabled');
					}
					else {
						//Disable description
						description.classList.add('disabled');
					}
				}
			}
		});

		// Trigger initial update to display the description for the default selected option
		select.dispatchEvent(new Event('change'));
	});
});

// For loading dynamic content into pre elements
document.addEventListener("DOMContentLoaded", async () => {
	const preElements = document.querySelectorAll('pre[dynamic-text-content]');

	preElements.forEach(async (preElement) => {
		const url = preElement.getAttribute('dynamic-text-content');

		try {
			const response = await fetch(url);
			if (!response.ok) {
				throw new Error(`Network response was not ok: ${response.statusText}`);
			}
			const data = await response.text();
			preElement.textContent = data;
		} catch (error) {
			console.error('Error fetching the content:', error);
			preElement.textContent = `Failed to load content: ${error.message}`;
		}
	});
});

// For settings page "browse" buttons: open the native folder picker and fill the selected path into the target input
async function browseForFolder(inputId) {
	try {
		const response = await fetch('/Dialogs/OpenFolder');
		const path = await response.json();
		if (path) {
			document.getElementById(inputId).value = path;
		}
	} catch (error) {
		console.error('Error fetching the folder path:', error);
	}
}

// 选择的是单个文件而非文件夹，因此使用 /Dialogs/OpenFile 接口
async function browseForFile(inputId) {
	try {
		const response = await fetch('/Dialogs/OpenFile');
		const path = await response.json();
		if (path) {
			document.getElementById(inputId).value = path;
		}
	} catch (error) {
		console.error('Error fetching the file path:', error);
	}
}

// 设置页多选组的「全选 / 全不选」按钮：按 name 批量切换同名复选框。
// 用 name 而非父容器定位，是为了与后端按字段名读取表单值的逻辑保持一致 ——
// 后端只认 name，前端就不应依赖 DOM 结构，避免改版式时按钮失效。
function setCheckBoxGroup(groupName, checked) {
	document.querySelectorAll(`input[type="checkbox"][name="${groupName}"]`).forEach(function (input) {
		input.checked = checked;
	});
}